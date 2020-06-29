using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClangSharpTest2020
{
    public sealed partial class TranslatedFile : IDeclarationContainer
    {
        public TranslatedLibrary Library { get; }
        TranslatedFile IDeclarationContainer.File => this;

        /// <summary>Declarations for which <see cref="TranslatedDeclaration.CanBeRoot"/> is true.</summary>
        private readonly List<TranslatedDeclaration> IndependentDeclarations = new List<TranslatedDeclaration>();
        /// <summary>Declarations for which <see cref="TranslatedDeclaration.CanBeRoot"/> is false.</summary>
        private readonly List<TranslatedDeclaration> LooseDeclarations = new List<TranslatedDeclaration>();
        public bool IsEmptyTranslation => IndependentDeclarations.Count == 0 && LooseDeclarations.Count == 0;

        public string FilePath { get; }
        private IntPtr FileHandle { get; }

        private readonly List<TranslationDiagnostic> _Diagnostics = new List<TranslationDiagnostic>();
        public ReadOnlyCollection<TranslationDiagnostic> Diagnostics { get; }

        /// <summary>The name of the type which will contain the declarations from <see cref="LooseDeclarations"/>.</summary>
        private string LooseDeclarationsTypeName { get; }

        /// <summary>True if <see cref="Diagnostics"/> contains any diagnostic with <see cref="TranslationDiagnostic.IsError"/> or true.</summary>
        public bool HasErrors { get; private set; }

        internal TranslatedFile(TranslatedLibrary library, IntPtr fileHandle, string filePath)
        {
            Library = library;
            FileHandle = fileHandle;
            FilePath = filePath;
            Diagnostics = _Diagnostics.AsReadOnly();

            LooseDeclarationsTypeName = Path.GetFileNameWithoutExtension(FilePath);
        }

        void IDeclarationContainer.AddDeclaration(TranslatedDeclaration declaration)
        {
            Debug.Assert(ReferenceEquals(declaration.Parent, this));

            if (declaration.CanBeRoot)
            { IndependentDeclarations.Add(declaration); }
            else
            { LooseDeclarations.Add(declaration); }
        }

        void IDeclarationContainer.RemoveDeclaration(TranslatedDeclaration declaration)
        {
            Debug.Assert(ReferenceEquals(declaration.Parent, this));
            bool removed;

            if (declaration.CanBeRoot)
            { removed = IndependentDeclarations.Remove(declaration); }
            else
            { removed = LooseDeclarations.Remove(declaration); }

            Debug.Assert(removed);
        }

        public IEnumerator<TranslatedDeclaration> GetEnumerator()
            => IndependentDeclarations.Union(LooseDeclarations).GetEnumerator();

        public void Validate()
        {
            if (IsEmptyTranslation)
            { Diagnostic(Severity.Note, new SourceLocation(FilePath), "File did not result in anything to be translated."); }

            //TODO: This should use the transformation infrastructure.
            // Associate loose declarations (IE: global functions and variables) to a record matching our file name if we have one.
            if (LooseDeclarations.Count > 0)
            {
                // If any of the loose declarations have the same name as our loose declarations type, modify the name to avoid CS0542: member names cannot be the same as their enclosing type
                foreach (TranslatedDeclaration declaration in LooseDeclarations)
                {
                    if (declaration.TranslatedName == LooseDeclarationsTypeName)
                    {
                        Diagnostic(Severity.Note, declaration.Declaration, $"Renaming loose declaration '{declaration}' to avoid conflicting with containing type.");
                        declaration.TranslatedName += "_";
                    }
                }

                //TODO: This would be problematic for enums which are named LooseDeclarationsTypeName since we'd double-write the file.
                TranslatedRecord looseDeclarationsTarget = IndependentDeclarations.OfType<TranslatedRecord>().FirstOrDefault(r => r.TranslatedName == LooseDeclarationsTypeName);
                if (looseDeclarationsTarget is object)
                {
                    while (LooseDeclarations.Count > 0)
                    { LooseDeclarations[0].Parent = looseDeclarationsTarget; }
                }
            }

            foreach (TranslatedDeclaration declaration in this)
            { declaration.Validate(); }
        }

        private void TranslateLooseDeclarations(CodeWriter writer)
        {
            // If there are no loose declarations, there's nothing to do.
            if (LooseDeclarations.Count == 0)
            { return; }

            // Write out a static class containing all of the loose declarations
            writer.EnsureSeparation();
            writer.WriteLine($"public static unsafe partial class {LooseDeclarationsTypeName}");
            using (writer.Block())
            {
                foreach (TranslatedDeclaration declaration in LooseDeclarations)
                { declaration.Translate(writer); }
            }
        }

        public void Translate()
        {
            // Translate loose declarations
            if (LooseDeclarations.Count > 0)
            {
                using CodeWriter writer = new CodeWriter();
                TranslateLooseDeclarations(writer);
                writer.WriteOut($"{LooseDeclarationsTypeName}.cs");
            }

            // Translate independent declarations
            foreach (TranslatedDeclaration declaration in IndependentDeclarations)
            {
                // Don't create files for dummy declarations
                if (declaration.IsDummy)
                { continue; }

                using CodeWriter writer = new CodeWriter();
                declaration.Translate(writer);
                writer.WriteOut($"{declaration.TranslatedName}.cs");
            }
        }

        public void Translate(CodeWriter writer)
        {
            // Translate loose declarations
            TranslateLooseDeclarations(writer);

            // Translate independent declarations
            foreach (TranslatedDeclaration declaration in IndependentDeclarations)
            { declaration.Translate(writer); }
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
            => Diagnostic(new TranslationDiagnostic(location, severity, message));

        internal void Diagnostic(Severity severity, string message)
            => Diagnostic(severity, new SourceLocation(FilePath), message);

        internal void Diagnostic(Severity severity, Cursor associatedCursor, string message)
            => Diagnostic(severity, new SourceLocation(associatedCursor.Extent.Start), message);

        internal void Diagnostic(Severity severity, CXCursor associatedCursor, string message)
            => Diagnostic(severity, new SourceLocation(associatedCursor.Extent.Start), message);

        internal void ProcessCursorChildren(IDeclarationContainer container, Cursor cursor)
        {
            foreach (Cursor child in cursor.CursorChildren)
            { ProcessCursor(container, child); }
        }

        internal void ProcessCursor(IDeclarationContainer container, Cursor cursor)
        {
            // Warn if the cursor doesn't actually belong to this file
            CXFile cursorFile = cursor.Extent.Start.GetFileLocation();
            Debug.Assert(cursor.Extent.End.GetFileLocation().Handle == cursorFile.Handle, "The start and end file handles of a cursor should match.");

            if (cursorFile.Handle != FileHandle)
            { Diagnostic(Severity.Warning, $"Cursor from '{cursorFile.Name}' found while processing cursors from '{FilePath}'."); }

            //---------------------------------------------------------------------------------------------------------
            // Skip cursors which explicitly do not have translation implemented.
            // This needs to happen first in case some of these checks overlap with cursors which are translated.
            // (For instance, class template specializatiosn are records.)
            //---------------------------------------------------------------------------------------------------------
            if (IsExplicitlyUnsupported(cursor))
            {
                Diagnostic(Severity.Ignored, cursor, $"{cursor.CursorKindDetailed()} aren't supported yet.");
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
                Debug.Assert(false, "This method should not be used to process translation unit declarations.");
                Debug.Assert(container is TranslatedFile, "Translation units should only occur within the root declaration container.");
                ProcessCursorChildren(container, cursor);
                return;
            }

            // Ignore linkage specification (IE: `exern "C"`)
            if (cursor.Handle.DeclKind == CX_DeclKind.CX_DeclKind_LinkageSpec)
            {
                ProcessCursorChildren(container, cursor);
                return;
            }

            // Ignore unimportant (to us) attributes on declarations
            if (cursor is Attr attribute)
            {
                switch (attribute.Kind)
                {
                    case CX_AttrKind.CX_AttrKind_DLLExport:
                    case CX_AttrKind.CX_AttrKind_DLLImport:
                    case CX_AttrKind.CX_AttrKind_Aligned:
                        break;
                    default:
                        Diagnostic(Severity.Warning, attribute, $"Attribute of unrecognized kind: {attribute.Kind}");
                        break;
                }

                return;
            }

            // Namespace using directives do not impact the output
            if (cursor is UsingDirectiveDecl)
            { return; }

            // Namespace aliases do not impact the output
            if (cursor is NamespaceAliasDecl)
            { return; }

            // Friend declarations don't really mean anything to C#
            // They're usually implementation details anyway.
            if (cursor is FriendDecl)
            { return; }

            // Base specifiers are discovered by the record layout
            if (cursor is CXXBaseSpecifier)
            { return; }

            // Access specifiers are discovered by inspecting the member directly
            if (cursor is AccessSpecDecl)
            { return; }

            // Empty declarations (semi-colons with no corresponding declaration or statement) have no impact on the output
            if (cursor is EmptyDecl)
            {
                Debug.Assert(cursor.CursorChildren.Count == 0, "Empty declarations should not have children.");
                ProcessCursorChildren(container, cursor);
                return;
            }

            //---------------------------------------------------------------------------------------------------------
            // Cursors which only affect the context
            //---------------------------------------------------------------------------------------------------------

            // Namespaces
            if (cursor is NamespaceDecl namespaceDeclaration)
            {
                Debug.Assert(container is TranslatedFile, "Namespaces should only occur within the root declaration container.");
                ProcessCursorChildren(container, cursor);
                return;
            }

            //---------------------------------------------------------------------------------------------------------
            // Records, enums, and loose functions
            //---------------------------------------------------------------------------------------------------------

            // Handle records (classes, structs, and unions)
            if (cursor is RecordDecl record)
            {
                // Ignore forward-declarations
                if (!record.Handle.IsDefinition)
                {
                    // Unless this is the canonical forward-declaration and there's no definition
                    // (This is sometimes used for types which are visible on the public API but provide no public API themselves.)
                    // (Clang seems to always declare the 1st declaration of a record the canonical one.)
                    if (record.IsCanonicalDecl && record.Definition is null)
                    { new TranslatedUndefinedRecord(container, record); }

                    return;
                }

                new TranslatedRecord(container, record);
                return;
            }

            // Handle enums
            if (cursor is EnumDecl enumDeclaration)
            {
                new TranslatedEnum(container, enumDeclaration);
                return;
            }

            // Handle functions and methods
            if (cursor is FunctionDecl function)
            {
                // Ignore non-canonical function declarations
                // (This ignores things like the actual definition of a inline method declared earlier with the record's declaration.)
                if (!function.IsCanonicalDecl)
                {
                    Diagnostic(Severity.Ignored, function, "Ignored non-canonical function declaration.");
                    return;
                }

                new TranslatedFunction(container, function);
                return;
            }

            // Handle fields
            // This method is not meant to handle fields (they are enumerated by TranslatedRecord when querying the record layout.)
            if (cursor is FieldDecl field)
            {
                Diagnostic(Severity.Warning, field, "Field declaration processed outside of record.");
                return;
            }

            // Handle static fields and globals
            //TODO: Constants need special treatment here.
            if (cursor is VarDecl variable)
            {
                new TranslatedStaticField(container, variable);
                return;
            }

            //---------------------------------------------------------------------------------------------------------
            // Misc
            //---------------------------------------------------------------------------------------------------------
            if (cursor is TypedefDecl typedef)
            {
                new TranslatedTypedef(container, typedef);
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

            // If we got this far, the cursor might be supported
            return false;
        }

        string IDeclarationContainer.GetNameForUnnamed(string category)
            // Names of declarations at the file level should be library-unique, so the library is responsible for naming unnamed things.
            => Library.GetNameForUnnamed(category);
    }
}
