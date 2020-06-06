// This is disabled right now because it complains about anonyomous types and nested types used immediately for a field's type.
//#define WARN_WHEN_CURSORS_ARE_PROCESSED_MULTIPLE_TIMES
using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClangSharpTest2020
{
    public sealed class TranslatedFile : IDisposable
    {
        public TranslatedLibrary Library { get; }

        private readonly List<TranslatedRecord> Records = new List<TranslatedRecord>();
        private readonly List<TranslatedFunction> LooseFunctions = new List<TranslatedFunction>();

        public string FilePath { get; }
        private readonly TranslationUnit TranslationUnit;

        private readonly List<TranslationDiagnostic> _Diagnostics = new List<TranslationDiagnostic>();
        public ReadOnlyCollection<TranslationDiagnostic> Diagnostics { get; }

        private readonly HashSet<Cursor> AllCursors = new HashSet<Cursor>();
        private readonly HashSet<Cursor> UnprocessedCursors;

        /// <summary>True if <see cref="Diagnostics"/> contains any diagnostic with <see cref="TranslationDiagnostic.IsError"/> or true.</summary>
        public bool HasErrors { get; private set; }

        internal TranslatedFile(TranslatedLibrary library, CXIndex index, string filePath)
        {
            Library = library;
            FilePath = filePath;
            Diagnostics = _Diagnostics.AsReadOnly();

            // These are the flags used by ClangSharp.PInvokeGenerator, so we're just gonna use them for now.
            CXTranslationUnit_Flags translationFlags =
                CXTranslationUnit_Flags.CXTranslationUnit_IncludeAttributedTypes
            ;

            CXTranslationUnit unitHandle;
            CXErrorCode status = CXTranslationUnit.TryParse(index, FilePath, Library.ClangCommandLineArguments, ReadOnlySpan<CXUnsavedFile>.Empty, translationFlags, out unitHandle);

            if (status != CXErrorCode.CXError_Success)
            {
                Diagnostic(Severity.Fatal, $"Failed to parse source file due to Clang error {status}.");
                return;
            }

            try
            {
                if (unitHandle.NumDiagnostics != 0)
                {
                    for (uint i = 0; i < unitHandle.NumDiagnostics; i++)
                    {
                        using CXDiagnostic diagnostic = unitHandle.GetDiagnostic(i);
                        Diagnostic(diagnostic);
                    }

                    if (HasErrors)
                    {
                        Diagnostic(Severity.Fatal, "Aborting translation due to previous errors.");
                        unitHandle.Dispose();
                        return;
                    }
                }
            }
            catch
            {
                unitHandle.Dispose();
                throw;
            }

            // Create the translation unit
            TranslationUnit = TranslationUnit.GetOrCreate(unitHandle);

            // Enumerate all cursors and mark them as unprocessed (used for sanity checks)
            EnumerateAllCursorsRecursive(TranslationUnit.TranslationUnitDecl);
            UnprocessedCursors = new HashSet<Cursor>(AllCursors);

            // Process the translation unit
            ProcessCursor(ImmutableArray<TranslationContext>.Empty, TranslationUnit.TranslationUnitDecl);

            // Translate global functions
            if (LooseFunctions.Count > 0)
            {
                string globalFunctionType = Path.GetFileNameWithoutExtension(FilePath);
                TranslatedRecord globalFunctionTarget = Records.FirstOrDefault(r => r.Record.Name == globalFunctionType);

                if (globalFunctionTarget is object)
                {
                    foreach (TranslatedFunction function in LooseFunctions)
                    { globalFunctionTarget.AddAsStaticMethod(function); }
                }
                else
                {
                    using CodeWriter writer = new CodeWriter();
                    writer.WriteLine($"public partial static {globalFunctionType}");
                    using (writer.Block())
                    {
                        foreach (TranslatedFunction function in LooseFunctions)
                        { function.Translate(writer); }
                    }

                    writer.WriteOut($"{globalFunctionType}.cs");
                }
            }

            // Perform the translation
            foreach (TranslatedRecord record in Records)
            { record.Translate(); }

            // Note unprocessed cursors
#if false //TODO: Re-enable this
            foreach (Cursor cursor in UnprocessedCursors)
            { Diagnostic(Severity.Warning, cursor, $"{cursor.CursorKindDetailed()} was not processed."); }
#endif
        }

        private void Diagnostic(in TranslationDiagnostic diagnostic)
        {
            _Diagnostics.Add(diagnostic);

            if (diagnostic.IsError)
            { HasErrors = true; }

            // Send the diagnostic to the library
            Library.Diagnostic(diagnostic);
        }

        internal void Diagnostic(Severity severity, SourceLocation location, string message)
            => Diagnostic(new TranslationDiagnostic(this, location, severity, message));

        internal void Diagnostic(Severity severity, string message)
            => Diagnostic(severity, new SourceLocation(FilePath), message);

        private void Diagnostic(CXDiagnostic clangDiagnostic)
            => Diagnostic(new TranslationDiagnostic(this, clangDiagnostic));

        internal void Diagnostic(Severity severity, Cursor associatedCursor, string message)
            => Diagnostic(severity, new SourceLocation(associatedCursor.Extent.Start), message);

        internal void Diagnostic(Severity severity, CXCursor associatedCursor, string message)
            => Diagnostic(severity, new SourceLocation(associatedCursor.Extent.Start), message);

        private void EnumerateAllCursorsRecursive(Cursor cursor)
        {
            // Only add cursors from the main file to AllCursors
            //PERF: Skip this if the parent node wasn't from the main file
            if (cursor.IsFromMainFile())
            { AllCursors.Add(cursor); }

            // Add all children, recursively
            foreach (Cursor child in cursor.CursorChildren)
            { EnumerateAllCursorsRecursive(child); }
        }

        internal void Consume(Cursor cursor)
        {
            if (cursor.TranslationUnit != TranslationUnit)
            { throw new InvalidOperationException("The file should not attempt to consume cursors from other translation units."); }

            if (!UnprocessedCursors.Remove(cursor))
            {
                if (AllCursors.Contains(cursor))
                {
#if WARN_WHEN_CURSORS_ARE_PROCESSED_MULTIPLE_TIMES
                    // Only warn if the cursor is a declaration, a statement, or an untyped cursor.
                    // This idea here is to only warn for cursors which affect behavior or API.
                    // This avoids issues like when a type reference is shared between multiple cursors, such as `int i, j;`-type variable declarations.
                    if (cursor is Decl || cursor is Stmt || cursor.GetType() == typeof(Cursor))
                    { Diagnostic(Severity.Warning, cursor, $"{cursor.CursorKindDetailed()} cursor was processed more than once."); }
#endif
                }
                else if (cursor.TranslationUnit != TranslationUnit)
                { Diagnostic(Severity.Error, cursor, $"{cursor.CursorKindDetailed()} cursor was processed from an external translation unit."); }
                else
                {
                    // We shouldn't process cursors that come from outside of our file.
                    // Note: This depends on Cursor.IsFromMainFile using pathogen_Location_isFromMainFile because otherwise macro expansions will trigger this.
                    // Note: We can't only rely on the cursor having been in the AllCursors list because there are some oddball cursors that are part of the translation unit but aren't part of the AST.
                    //       One example of such a cursor is the one on the word `union` in an anonymous, fieldless union in a struct.
                    if (!cursor.IsFromMainFile())
                    { Diagnostic(Severity.Warning, cursor, $"{cursor.CursorKindDetailed()} cursor from outside our fle was processed."); }

                    // If we consume a cursor which we didn't consider to be a part of this file, we add it to our list of
                    // all cursors to ensure our double cursor consumption above works for them.
                    AllCursors.Add(cursor);
                }
            }
        }

        internal void Consume(CXCursor cursorHandle)
            => Consume(FindCursor(cursorHandle));

        internal void ConsumeRecursive(Cursor cursor)
        {
            Consume(cursor);

            foreach (Cursor child in cursor.CursorChildren)
            { ConsumeRecursive(child); }
        }

        internal void ConsumeRecursive(CXCursor cursorHandle)
            => ConsumeRecursive(FindCursor(cursorHandle));

        /// <remarks>Same as consume, but indicates that the cursor has no affect on the translation output.</remarks>
        internal void Ignore(Cursor cursor)
            => Consume(cursor);

        /// <remarks>Same as consume, but indicates that the cursor has no affect on the translation output.</remarks>
        internal void IgnoreRecursive(Cursor cursor)
            => ConsumeRecursive(cursor);

        internal void ProcessCursorChildren(ImmutableArray<TranslationContext> context, Cursor cursor)
        {
            foreach (Cursor child in cursor.CursorChildren)
            { ProcessCursor(context, child); }
        }

        internal void ProcessUnconsumeChildren(ImmutableArray<TranslationContext> context, Cursor cursor)
        {
            foreach (Cursor child in cursor.CursorChildren)
            {
                if (UnprocessedCursors.Contains(child))
                { ProcessCursor(context, child); }
            }
        }

        internal void ProcessCursor(ImmutableArray<TranslationContext> context, Cursor cursor)
        {
            // Skip cursors outside of the specific file being processed
            if (!cursor.IsFromMainFile())
            { return; }

            //---------------------------------------------------------------------------------------------------------
            // Skip cursors which explicitly do not have translation implemented.
            // This needs to happen first in case some of these checks overlap with cursors which are translated.
            // (For instance, class template specializatiosn are records.)
            //---------------------------------------------------------------------------------------------------------
            if (IsExplicitlyUnsupported(cursor))
            {
                Diagnostic(Severity.Ignored, cursor, $"{cursor.CursorKindDetailed()} aren't supported yet.");
                IgnoreRecursive(cursor);
                return;
            }

            //---------------------------------------------------------------------------------------------------------
            // Cursors which do not have a direct impact on the output
            // (These cursors are usually just containers for other cursors or the information
            //  they provide is already available on the cursors which they affect.)
            //---------------------------------------------------------------------------------------------------------

            // For translation units, just process all the children
            if (cursor is TranslationUnitDecl)
            {
                Debug.Assert(context.Length == 0);
                Ignore(cursor);
                ProcessCursorChildren(context, cursor);
                return;
            }

            // Ignore linkage specification (IE: `exern "C"`)
            if (cursor.Handle.DeclKind == CX_DeclKind.CX_DeclKind_LinkageSpec)
            {
                Ignore(cursor);
                ProcessCursorChildren(context, cursor);
                return;
            }

            // Ignore unimportant (to us) attributes on declarations
            if (cursor is Decl decl)
            {
                foreach (Attr attribute in decl.Attrs)
                {
                    switch (attribute.Kind)
                    {
                        case CX_AttrKind.CX_AttrKind_DLLExport:
                        case CX_AttrKind.CX_AttrKind_DLLImport:
                            Ignore(attribute);
                            break;
                    }
                }
            }

            // Namespace using directives do not impact the output
            if (cursor is UsingDirectiveDecl)
            {
                IgnoreRecursive(cursor);
                return;
            }

            // Namespace aliases do not impact the output
            if (cursor is NamespaceAliasDecl)
            {
                IgnoreRecursive(cursor);
                return;
            }

            // Friend declarations don't really mean anything to C#
            // They're usually implementation details anyway.
            if (cursor is FriendDecl)
            {
                IgnoreRecursive(cursor);
                return;
            }

            //---------------------------------------------------------------------------------------------------------
            // Cursors which only affect the context
            //---------------------------------------------------------------------------------------------------------

            // Namespaces
            if (cursor is NamespaceDecl namespaceDeclaration)
            {
                Consume(cursor);
                ProcessCursorChildren(context.Add(namespaceDeclaration), cursor);
                return;
            }

            //---------------------------------------------------------------------------------------------------------
            // Records and loose functions
            //---------------------------------------------------------------------------------------------------------

            // Handle records (classes, structs, and unions)
            if (cursor is RecordDecl record)
            {
                // Ignore forward-declarations
                if (!record.Handle.IsDefinition)
                {
                    Ignore(cursor);
                    return;
                }

                Records.Add(new TranslatedRecord(context, this, record));
                return;
            }

            // Handle loose functions
            if (cursor is FunctionDecl function)
            {
                LooseFunctions.Add(new TranslatedFunction(context, this, function));
                return;
            }

            //---------------------------------------------------------------------------------------------------------
            // Failure
            //---------------------------------------------------------------------------------------------------------

            // If we got this far, we didn't know how to process the cursor
            // At one point we processed the children of the cursor anyway, but this can lead to confusing behavior when the skipped cursor provided meaningful context.
            Diagnostic(Severity.Warning, cursor, $"Not sure how to process cursor of type {cursor.CursorKindDetailed()}.");
        }

        private static bool IsExplicitlyUnsupported(Cursor cursor)
        {
            // Ignore template specializations
            if (cursor is ClassTemplateSpecializationDecl)
            { return true; }

            // Ignore templates
            if (cursor is TemplateDecl)
            { return true; }

            // Ignore typedefs
            // Typedefs will probably almost always have to be a special case.
            // Sometimes they aren't very meaningful to the translation, and sometimes they have a large impact on how the API is used.
            if (cursor is TypedefDecl)
            { return true; }

            // Can't translate global variables yet
            //TODO: Constants deserve special treatment here.
            if (cursor is VarDecl)
            { return true; }

            // Enums aren't supported yet
            if (cursor is EnumDecl)
            { return true; }

            // If we got this far, the cursor might be supported
            return false;
        }

        public Cursor FindCursor(CXCursor cursorHandle)
        {
            if (cursorHandle.IsNull)
            {
                Diagnostic(Severity.Warning, $"Someone tried to get the Cursor for a null handle.");
                return null;
            }

            return TranslationUnit.GetOrCreate(cursorHandle);
        }

        public void Dispose()
            => TranslationUnit?.Dispose();
    }
}
