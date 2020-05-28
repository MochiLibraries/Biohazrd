using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ClangSharpTest2020
{
    public sealed class TranslatedFile : IDisposable
    {
        public TranslatedLibrary Library { get; }
        public string FilePath { get; }
        private readonly TranslationUnit TranslationUnit;

        private readonly List<TranslationDiagnostic> _Diagnostics = new List<TranslationDiagnostic>();
        private readonly ReadOnlyCollection<TranslationDiagnostic> Diagnostics;

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
                CXTranslationUnit_Flags.CXTranslationUnit_IncludeAttributedTypes |
                CXTranslationUnit_Flags.CXTranslationUnit_VisitImplicitAttributes
            ;

            CXTranslationUnit unitHandle;
            CXErrorCode status = CXTranslationUnit.TryParse(index, FilePath, Library.ClangCommandLineArguments, ReadOnlySpan<CXUnsavedFile>.Empty, translationFlags, out unitHandle);

            if (status != CXErrorCode.CXError_Success)
            {
                HandleDiagnostic(TranslationDiagnosticSeverity.Fatal, $"Failed to parse source file due to Clang error {status}.");
                return;
            }

            try
            {
                if (unitHandle.NumDiagnostics != 0)
                {
                    for (uint i = 0; i < unitHandle.NumDiagnostics; i++)
                    {
                        using CXDiagnostic diagnostic = unitHandle.GetDiagnostic(i);
                        HandleDiagnostic(diagnostic);
                    }

                    if (HasErrors)
                    {
                        HandleDiagnostic(TranslationDiagnosticSeverity.Fatal, "Aborting translation due to previous errors.");
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
            ProcessCursor(TranslationUnit.TranslationUnitDecl);

            // Note unprocessed cursors
            foreach (Cursor cursor in UnprocessedCursors)
            { HandleDiagnostic(TranslationDiagnosticSeverity.Warning, cursor, $"{cursor.CursorKindDetailed()} was not processed."); }
        }

        private void HandleDiagnostic(in TranslationDiagnostic diagnostic)
        {
            _Diagnostics.Add(diagnostic);

            if (diagnostic.IsError)
            { HasErrors = true; }

            // Send the diagnostic to the library
            Library.HandleDiagnostic(diagnostic);
        }

        private void HandleDiagnostic(TranslationDiagnosticSeverity severity, SourceLocation location, string message)
            => HandleDiagnostic(new TranslationDiagnostic(this, location, severity, message));

        private void HandleDiagnostic(TranslationDiagnosticSeverity severity, string message)
            => HandleDiagnostic(severity, new SourceLocation(FilePath), message);

        private void HandleDiagnostic(CXDiagnostic clangDiagnostic)
            => HandleDiagnostic(new TranslationDiagnostic(this, clangDiagnostic));

        private void HandleDiagnostic(TranslationDiagnosticSeverity severity, Cursor associatedCursor, string message)
            => HandleDiagnostic(severity, new SourceLocation(associatedCursor.Extent.Start), message);

        private void EnumerateAllCursorsRecursive(Cursor cursor)
        {
            // Skip cursors outside of the specific file being processed
            if (!cursor.IsFromMainFile())
            { return; }

            AllCursors.Add(cursor);

            // Add all children, recursively
            foreach (Cursor child in cursor.CursorChildren)
            { EnumerateAllCursorsRecursive(child); }
        }

        private void MarkAsProcessed(Cursor cursor)
        {
            if (!UnprocessedCursors.Remove(cursor))
            {
                if (AllCursors.Contains(cursor))
                { HandleDiagnostic(TranslationDiagnosticSeverity.Warning, cursor, $"{cursor.CursorKindDetailed()} cursor was processed more than once."); }
                else
                { HandleDiagnostic(TranslationDiagnosticSeverity.Error, cursor, $"Tried to mark a {cursor.CursorKindDetailed()} cursor as processed when it came from outside this file."); }
            }
        }

        private void MarkAsProcessedRecursive(Cursor cursor)
        {
            MarkAsProcessed(cursor);

            foreach (Cursor child in cursor.CursorChildren)
            {
                // It's possible for children of main file cursors to be from outside the main file
                // when macros are being used. So we don't mark non-main children here.
                if (!child.IsFromMainFile())
                { continue; }

                MarkAsProcessedRecursive(child);
            }
        }

        private void ProcessCursorChildren(Cursor cursor)
        {
            foreach (Cursor child in cursor.CursorChildren)
            { ProcessCursor(child); }
        }

        private void ProcessCursor(Cursor cursor)
        {
            // Skip cursors outside of the specific file being processed
            // For some reason the first declaration in a file will only have its end marked as being from the main file.
            if (!cursor.Extent.Start.IsFromMainFile && !cursor.Extent.End.IsFromMainFile)
            { return; }

            // For translation units, just process all the children
            if (cursor is TranslationUnitDecl)
            {
                MarkAsProcessed(cursor);
                ProcessCursorChildren(cursor);
                return;
            }

            // Ignore linkage specification (IE: `exern "C"`)
            if (cursor.Handle.DeclKind == CX_DeclKind.CX_DeclKind_LinkageSpec)
            {
                MarkAsProcessed(cursor);
                ProcessCursorChildren(cursor);
                return;
            }

            //TODO: Do we need to keep track of namespaces for context?
            if (cursor is NamespaceDecl)
            {
                MarkAsProcessed(cursor);
                ProcessCursorChildren(cursor);
                return;
            }

            // Ignore unimportant (to us) attributes
            if (cursor is Decl decl)
            {
                foreach (Attr attribute in decl.Attrs)
                {
                    switch (attribute.Kind)
                    {
                        case CX_AttrKind.CX_AttrKind_DLLExport:
                        case CX_AttrKind.CX_AttrKind_DLLImport:
                            MarkAsProcessed(attribute);
                            break;
                    }
                }
            }

            if (cursor is RecordDecl record)
            {
                // Ignore forward-declarations
                if (!record.Handle.IsDefinition)
                {
                    MarkAsProcessed(cursor);
                    return;
                }

                //TODO: Remove this quick and dirty handle check
                foreach (FieldDecl field in record.Fields)
                { MarkAsProcessedRecursive(field); }

                if (record is CXXRecordDecl cxxRecord)
                {
                    // Methods also includes the destructor
                    foreach (CXXMethodDecl method in cxxRecord.Methods)
                    { ProcessCursor(method); }

                    foreach (CXXConstructorDecl ctor in cxxRecord.Ctors)
                    { MarkAsProcessedRecursive(ctor); }
                }

                // Swallow any access specifiers
                // We don't need them because their information is encoded on individual methods and fields
                foreach (Cursor child in record.CursorChildren)
                {
                    if (child is AccessSpecDecl)
                    { MarkAsProcessed(child); }
                }

                MarkAsProcessed(cursor);
                return;
            }

            if (cursor is FunctionDecl function)
            {
                foreach (ParmVarDecl parameter in function.Parameters)
                { MarkAsProcessedRecursive(parameter); }

                // If the function has a body, it is ignored (for now.)
                //TODO: Do best effort translation of simple inline functions.
                if (function.Body != null)
                { MarkAsProcessedRecursive(function.Body); }

                // Ignore children which are type references, they belong to the return type
                foreach (Cursor child in cursor.CursorChildren)
                {
                    if (child.CursorKind == CXCursorKind.CXCursor_NamespaceRef || child.CursorKind == CXCursorKind.CXCursor_TypeRef)
                    { MarkAsProcessed(child); }
                }

                MarkAsProcessed(cursor);
                return;
            }

            // If we got this far, we didn't know how to process the cursor
            HandleDiagnostic(TranslationDiagnosticSeverity.Warning, cursor, $"Not sure how to process cursor of type {cursor.CursorKindDetailed()}.");
            ProcessCursorChildren(cursor);
        }

        public void Dispose()
            => TranslationUnit?.Dispose();
    }
}
