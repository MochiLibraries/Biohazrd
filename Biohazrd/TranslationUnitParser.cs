using ClangSharp;
using ClangSharp.Interop;
using ClangSharp.Pathogen;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Biohazrd.TranslationUnitParser.CreateDeclarationsEnumerator;
using ClangType = ClangSharp.Type;

namespace Biohazrd
{
    internal sealed partial class TranslationUnitParser
    {
        private readonly TranslationUnit TranslationUnit;
        private readonly TranslationOptions Options;

        internal CodeGeneratorPool CodeGeneratorPool { get; }

        private readonly ImmutableArray<TranslationDiagnostic>.Builder ParsingDiagnosticsBuilder = ImmutableArray.CreateBuilder<TranslationDiagnostic>();

        // path => path, this is a dictionary so we can use the canonical name provided to this library rather than whatever Clang gives us.
        private readonly Dictionary<string, string> AllFilePaths;
        private readonly HashSet<string> UnusedFilePaths;

        private readonly ImmutableArray<TranslatedFile>.Builder FilesBuilder = ImmutableArray.CreateBuilder<TranslatedFile>();
        private readonly Dictionary<IntPtr, TranslatedFile> FileHandleToTranslatedFileLookup = new();
        private readonly HashSet<TranslatedFile> PromotedOutOfScopeFiles = new(ReferenceEqualityComparer.Instance);

        private readonly ImmutableList<TranslatedDeclaration>.Builder DeclarationsBuilder = ImmutableList.CreateBuilder<TranslatedDeclaration>();

        private readonly ImmutableArray<TranslatedMacro>.Builder MacrosBuilder;

        private readonly bool ParsingComplete = false;

        internal TranslationUnitParser(List<SourceFileInternal> sourceFiles, TranslationOptions options, TranslationUnit translationUnit)
        {
            TranslationUnit = translationUnit;
            Options = options;

            CodeGeneratorPool = new CodeGeneratorPool(TranslationUnit);

            // We treat file paths are case-insensitive (see https://github.com/InfectedLibraries/Biohazrd/issues/1)
            // This is technically only valid on case-insensitive file systems, but in practice it's uncommon for two files to have the same case-insensitive name even on systems which support it.
            // (Especially for code which may be checked into source control and cloned on other operating systems.)
            // When https://github.com/dotnet/runtime/issues/14321 is realized, we should consider normalizing the paths to actual case instead.
            UnusedFilePaths = new HashSet<string>(sourceFiles.Where(s => s.IsInScope).Select(s => s.FilePath), StringComparer.OrdinalIgnoreCase);
            AllFilePaths = new Dictionary<string, string>(UnusedFilePaths.Count, StringComparer.OrdinalIgnoreCase);

            foreach (string filePath in UnusedFilePaths)
            { AllFilePaths.Add(filePath, filePath); }

            // Create macro builder
            // Note that there are more preprocessor identifiers than there are macros because preprocessor identifiers are used for some non-macro things as well.
            // As such, this is an upper bound rather than a specific count.
            int maximumMacroCount = checked((int)PathogenExtensions.pathogen_GetPreprocessorIdentifierCount(TranslationUnit.Handle));
            MacrosBuilder = ImmutableArray.CreateBuilder<TranslatedMacro>(maximumMacroCount);

            // Process the translation unit
            ProcessTranslationUnit();

            // Process implicitly-instantiated specialized templates
            if (Options.EnableTemplateSupport)
            { ProcessImplicitlyInstantiatedSpecializedTemplates(); }

            // Process macros
            unsafe
            {
                [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
                static void EnumerateMacro(PathogenMacroInformation* macroInfo, void* userData)
                {
                    GCHandle thisHandle = GCHandle.FromIntPtr((IntPtr)userData);
                    Unsafe.As<TranslationUnitParser>(thisHandle.Target)!.ProcessMacro(macroInfo);
                }

                GCHandle thisHandle = default;
                try
                {
                    thisHandle = GCHandle.Alloc(this);
                    PathogenExtensions.pathogen_EnumerateMacros(TranslationUnit.Handle, &EnumerateMacro, (void*)GCHandle.ToIntPtr(thisHandle));
                }
                finally
                {
                    if (thisHandle.IsAllocated)
                    { thisHandle.Free(); }
                }
            }

            // Add null-handle files for any remaining unused files
            foreach (string filePath in UnusedFilePaths)
            { FilesBuilder.Add(new TranslatedFile(filePath, IntPtr.Zero, true)); }

            // Add all promoted out-of-scope files
            FilesBuilder.AddRange(PromotedOutOfScopeFiles);

            // Use the same sorting for the files as the input
            // (This is useful if a transformation later on needs to care about include file order.)
            {
                Dictionary<string, int> filePathIndices = new(sourceFiles.Count);
                for (int i = 0; i < sourceFiles.Count; i++)
                { filePathIndices.Add(sourceFiles[i].FilePath, i); }

                // ImmutableArray<T>.Builder.Sort uses Array.Sort which is not stable, so we need to ensure everything has an index
                foreach (TranslatedFile file in FilesBuilder)
                {
                    if (!filePathIndices.ContainsKey(file.FilePath))
                    {
                        Debug.Assert(!file.WasInScope, "Only promoted out-of-scope files should be missing at this point.");
                        filePathIndices.Add(file.FilePath, filePathIndices.Count);
                    }
                }

                int SortComparison(TranslatedFile a, TranslatedFile b)
                {
                    int ai = filePathIndices[a.FilePath];
                    int bi = filePathIndices[b.FilePath];
                    return ai.CompareTo(bi);
                }

                FilesBuilder.Sort(SortComparison);
            }

            // Mark ourselves as complete
            ParsingComplete = true;
        }

        internal Cursor FindCursor(CXCursor handle)
            => TranslationUnit.FindCursor(handle);

        internal ClangType FindType(CXType handle)
            => TranslationUnit.FindType(handle);

        private void ProcessTranslationUnit()
        {
            bool hasErrors = false;

            // Process diagnostics
            // The DiagnosticSet from a translation unit does not need to be disposed
            // Diagnostics do not need to be disposed, the fact that they can be disposed is left over from a legacy API.
            foreach (CXDiagnostic diagnostic in TranslationUnit.Handle.DiagnosticSet)
            {
                ParsingDiagnosticsBuilder.Add(new TranslationDiagnostic(diagnostic));

                if (diagnostic.Severity >= CXDiagnosticSeverity.CXDiagnostic_Error)
                { hasErrors = true; }
            }

            // If there's errors, don't try to process the cursors
            // Clang will still emit a best-effort cursor tree when there's errors, but they might not be coherent.
            if (hasErrors && !Options.TranslateEvenWithParsingErrors)
            { return; }

            // Process cursors
            foreach (Cursor cursor in TranslationUnit.TranslationUnitDecl.CursorChildren)
            { ProcessCursor(cursor); }
        }

        private TranslatedFile GetTranslatedFile(CXFile clangFile)
        {
            TranslatedFile? file;

            // Null file handles are considered to be synthesized
            // (This can occur for macros which are synthesized by Clang or defined on the command line.)
            if (clangFile.Handle == IntPtr.Zero)
            { return TranslatedFile.Synthesized; }

            // Check if the file is known to be in-scope
            if (FileHandleToTranslatedFileLookup.TryGetValue(clangFile.Handle, out file))
            { return file; }

            // Get the name of the file
            string fileName = clangFile.TryGetRealPathName().ToString();

            if (String.IsNullOrEmpty(fileName))
            { fileName = clangFile.Name.ToString(); }

            // Make sure we have the full path and that it's normalized.
            fileName = Path.GetFullPath(fileName);

            // See if we know about this file and it is unused.
            // If it isn't, this file is out of scope
            if (UnusedFilePaths.Remove(fileName) == false)
            {
                // We do not expect Clang to give us two different `CXFile`s with the same file name but different handles.
                Debug.Assert(!AllFilePaths.ContainsKey(fileName), $"'{fileName}' appeared in the translation unit multiple times with different {nameof(CXFile)} handles.");

                file = new TranslatedFile(fileName, clangFile.Handle, wasInScope: false);
                FileHandleToTranslatedFileLookup.Add(clangFile.Handle, file);
                return file;
            }

            // Create a new TranslatedFile to represent this file
            // Note that we look up the canonical name from AllFilePaths instead of using fileName directly
            // This ensures the file casing associated wtih TranslatedFile is consistent with what was passed to TranslatedLibraryBuilder.
            file = new TranslatedFile(AllFilePaths[fileName], clangFile.Handle, wasInScope: true);
            FileHandleToTranslatedFileLookup.Add(clangFile.Handle, file);
            FilesBuilder.Add(file);
            return file;
        }

        private TranslatedFile GetTranslatedFile(Cursor cursor)
        {
            // Look up the Clang file associated with the cursor
            CXFile cursorFile = cursor.Extent.Start.GetFileLocation();
            Debug.Assert(cursor.Extent.End.GetFileLocation().Handle == cursorFile.Handle, "The start and end file handles of a cursor should match.");
            return GetTranslatedFile(cursorFile);
        }

        private void ProcessCursor(Cursor cursor)
        {
            // Namespace and extern "C" declarations are completely ignored in Biohazrd
            // Their effect is already expressed on how they affect the actual declarations, and they cause unintentional scoping problems if we let them go to CreateDeclarations.
            // Note that this doesn't handle all namespace/extern "C" declarations since ones which are nested inside namespaces will be handled by CreateDeclarations.
            if (cursor is NamespaceDecl or Decl { Kind: CX_DeclKind.CX_DeclKind_LinkageSpec })
            {
                foreach (Cursor childCursor in cursor.CursorChildren)
                { ProcessCursor(childCursor); }

                return;
            }

            // Ignore cursors from system headers
            // (System headers in Clang are headers that come from specific files or have a special pragma. This does not mean "ignore #include <...>".)
            if (Options.SystemHeadersAreAlwaysOutOfScope && (cursor.Extent.Start.IsInSystemHeader || cursor.Extent.End.IsInSystemHeader))
            { return; }

            // Figure out what file this cursor comes from and if it is in scope
            TranslatedFile translatedFile = GetTranslatedFile(cursor);
            if (!translatedFile.WasInScope)
            {
                AssertAllChildrenOutOfScope(cursor);
                return;
            }

            // Process the cursor for the associated file
            DeclarationsBuilder.AddRange(CreateDeclarations(cursor, translatedFile));
        }

        internal CreateDeclarationsEnumerator CreateDeclarations(Cursor cursor, TranslatedFile file)
        {
            if (ParsingComplete)
            { throw new InvalidOperationException("This translation unit parser has already completed parsing."); }

            // Check if the cursor's file changed
            // This can happen if a file is included inside the scope of a declaration.
            // (Some libraries do this to reduce repetition with macros.)
            // (In theory it might be possible/safe to skip these declarations, but we keep them an issue a warning to err on the side of caution.)
            {
                CXFile cursorFile = cursor.Extent.Start.GetFileLocation();

                if (cursorFile.Handle != file.Handle)
                {
                    TranslatedFile newFile = GetTranslatedFile(cursor);

                    // If the file was not in scope, it is promoted to be partially in-scope for the pruposes of this declaration
                    // (This will not cause the file to be considered in-scope if it is included again outside of the parent declaration.)
                    // If this is the first time this file was promoted and the containing file was in-scope, issue a warning
                    if (newFile != TranslatedFile.Synthesized && !newFile.WasInScope && PromotedOutOfScopeFiles.Add(newFile) && file.WasInScope)
                    {
                        ParsingDiagnosticsBuilder.Add
                        (
                            Severity.Warning,
                            $"Out-of-scope file '{newFile.FilePath}' was included inside a declaration from in-scope file '{file.FilePath}'. " +
                            "The file was implicitly promoted to be in-scope in the context of the containing declaration."
                        );
                    }

                    file = newFile;
                }
            }

            switch (cursor)
            {
                //---------------------------------------------------------------------------------------------------------
                // Attributes
                //---------------------------------------------------------------------------------------------------------
                // We generally don't need to process attribute cursors since they can be enumerated as-needed by the
                // declarations they affect. We ignore ones we know don't impact the translation and warn for others.
                case Attr attribute:
                    switch (attribute.Kind)
                    {
                        case CX_AttrKind.CX_AttrKind_DLLExport:
                        case CX_AttrKind.CX_AttrKind_DLLImport:
                        case CX_AttrKind.CX_AttrKind_MSNoVTable:
                        case CX_AttrKind.CX_AttrKind_Uuid:
                        case CX_AttrKind.CX_AttrKind_Visibility:
                        case CX_AttrKind.CX_AttrKind_TypeVisibility:
                        //TODO: Alignment could impact the translation if types are allocated client-side.
                        case CX_AttrKind.CX_AttrKind_Aligned:
                            return None;
                        default:
                            ParsingDiagnosticsBuilder.Add(Severity.Warning, attribute, $"Attribute of unrecognized kind: {attribute.Kind}");
                            return None;
                    }
                //---------------------------------------------------------------------------------------------------------
                // Declarations
                //--------------------------------------------------------------------------------------------------------- 
                case Decl declaration:
                    switch (declaration)
                    {
                        //---------------------------------------------------------------------------------------------------------
                        // Cursors which are not expected ever
                        //---------------------------------------------------------------------------------------------------------
                        case TranslationUnitDecl:
                            throw new ArgumentException("Translation unit declarations must not reach this method.", nameof(cursor));

                        //---------------------------------------------------------------------------------------------------------
                        // Skip cursors which explicitly do not have translation implemented.
                        // This needs to happen first in case some of these checks overlap with cursors which are translated.
                        // (For instance, class template specializations are records.)
                        //---------------------------------------------------------------------------------------------------------
                        case ClassTemplatePartialSpecializationDecl:
                            return new TranslatedUnsupportedDeclaration
                            (
                                file,
                                declaration,
                                Severity.Warning,
                                $"Partial template specializations are not supported."
                            );
                        case ClassTemplateDecl classTemplate:
                            // Only translate the canonincal declaration
                            if (!classTemplate.IsCanonicalDecl)
                            { return None; }

                            return new TranslatedRecordTemplate(file, classTemplate);
                        case FunctionTemplateDecl functionTemplate:
                            // Only translate the canonincal declaration
                            if (!functionTemplate.IsCanonicalDecl)
                            { return None; }

                            return new TranslatedFunctionTemplate(file, functionTemplate);
                        case TemplateDecl template:
                            return new TranslatedUnsupportedTemplate(file, template);
                        case VarTemplatePartialSpecializationDecl:
                        case VarTemplateSpecializationDecl:
                            return new TranslatedUnsupportedDeclaration
                            (
                                file,
                                declaration,
                                Severity.Warning,
                                $"Templates are not supported."
                            );

                        //---------------------------------------------------------------------------------------------------------
                        // Declarations which we know how to handle
                        //---------------------------------------------------------------------------------------------------------
                        // Handle explicit template specializations (implicit instantiations are handled by ProcessImplicitlyInstantiatedSpecializedTemplates)
                        case ClassTemplateSpecializationDecl templateSpecialization:
                        {
                            if (!Options.EnableTemplateSupport)
                            {
                                return new TranslatedUnsupportedDeclaration
                                (
                                    file,
                                    declaration,
                                    Severity.Warning,
                                    $"Class templates are not supported when {nameof(TranslationOptions)}.{nameof(TranslationOptions.EnableTemplateSupport)} is false."
                                );
                            }

                            //TODO: This isn't consistent with normal records, but I'm not sure if it really ever happens in practice.
                            // There should be an implicit instantiation associated with this forward declaration which will be processed later.
                            if (templateSpecialization.IsCanonicalDecl && templateSpecialization.Definition is null)
                            { return None; }

                            return new TranslatedTemplateSpecialization(this, file, templateSpecialization);
                        }
                        // Handle records (classes, structs, and unions)
                        case RecordDecl record:
                        {
                            // Ignore forward-declarations
                            if (!record.Handle.IsDefinition)
                            {
                                // Unless this is the canonical forward-declaration and there's no definition
                                // (This is sometimes used for types which are visible on the public API but provide no public API themselves.)
                                // (Clang seems to always declare the 1st declaration of a record the canonical one.)
                                if (record.IsCanonicalDecl && record.Definition is null)
                                { return new TranslatedUndefinedRecord(file, record); }

                                return None;
                            }

                            return new TranslatedRecord(this, file, record);
                        }
                        // Handle functions and methods
                        case FunctionDecl function:
                        {
                            // Ignore non-canonical function declarations
                            // (This ignores things like the actual definition of a inline method declared earlier with the record's declaration.)
                            if (!function.IsCanonicalDecl)
                            { return None; }

                            return new TranslatedFunction(this, file, function);
                        }
                        // Handle enumerations
                        case EnumDecl enumDeclaration:
                            return new TranslatedEnum(file, enumDeclaration);
                        // Handle global variables, static fields, and constants
                        case VarDecl variable:
                            //TODO: Constants need special treatment here.
                            return new TranslatedStaticField(file, variable);
                        // Handle type definitions
                        case TypedefDecl typedef:
                            return new TranslatedTypedef(file, typedef);
                        // Handle type aliases (IE: `using MyIndex = uint32_t;`)
                        case TypeAliasDecl typeAlias:
                            return new TranslatedTypedef(file, typeAlias);

                        //---------------------------------------------------------------------------------------------------------
                        // Declarations we don't expect in this method
                        //---------------------------------------------------------------------------------------------------------
                        // Fields are handled in TranslatedRecord directly since it has access to layout information.
                        // As such, they should never reach this method.
                        case FieldDecl:
                            return new TranslatedUnsupportedDeclaration(file, declaration, Severity.Error, "Field declarations should not exist outside of records.");

                        //---------------------------------------------------------------------------------------------------------
                        // Container-ish declarations that we don't process directly
                        //---------------------------------------------------------------------------------------------------------
                        case NamespaceDecl:
                        case { Kind: CX_DeclKind.CX_DeclKind_LinkageSpec }: // extern "C"
                            // Note: extern "C" is mosly handled by ProcessCursor, but they can occur here if they're nested inside a namespace.
                            return CreateChildDeclarations(this, cursor, file);

                        //---------------------------------------------------------------------------------------------------------
                        // Declarations which have no impact on the translation whatsoever
                        //---------------------------------------------------------------------------------------------------------
                        // Namspace using directives don't affect the translation
                        case UsingDirectiveDecl:
                        // Namespace aliases don't affect the translation
                        case NamespaceAliasDecl:
                        // Friend declarations don't really mean anything to C#
                        // They're usually implementation details anyway.
                        case FriendDecl:
                        // Access specifiers are discovered by inspecting the member directly
                        case AccessSpecDecl:
                            return None;
                        // Empty declarations (semi-colons with no corresponding declaration or statement) have no impact on the output
                        case EmptyDecl:
                            Debug.Assert(cursor.CursorChildren.Count == 0, "Empty declarations should not have children.");
                            return None;

                        //---------------------------------------------------------------------------------------------------------
                        // For other types of declarations, just complain we don't support them
                        //---------------------------------------------------------------------------------------------------------
                        default:
                            return new TranslatedUnsupportedDeclaration
                            (
                                file,
                                declaration,
                                Severity.Warning,
                                $"Biohazrd doesn't know how to handle {declaration.CursorKindDetailed()} declarations."
                            );
                    }
                //---------------------------------------------------------------------------------------------------------
                // Misc cursors which have no impact on the output
                //---------------------------------------------------------------------------------------------------------
                // Base specifiers are discovered by the record layout
                case CXXBaseSpecifier:
                    return None;
                //---------------------------------------------------------------------------------------------------------
                // Cursors categories which we don't expect to process ever
                //---------------------------------------------------------------------------------------------------------
                case Stmt:
                    throw new ArgumentException("Statements cannot be processed by this method.", nameof(cursor));
                case PreprocessedEntity:
                    throw new ArgumentException("Preprocessed entities cannot be procssed by this method.", nameof(cursor));
                //---------------------------------------------------------------------------------------------------------
                // Failsafe for new cursor categories we aren't aware of
                //---------------------------------------------------------------------------------------------------------
                default:
                    ParsingDiagnosticsBuilder.Add(Severity.Warning, cursor, $"Biohazrd does not know how to handle '{cursor.CursorKindDetailed()}' cursors.");
                    return None;
            }
        }

        /// <summary>Asserts that every child of the given cursor is out-of-scope.</summary>
        /// <remarks>
        /// In theory this assert can fail if an in-scope file is included in the middle of another cursor.
        /// 
        /// For example:
        /// ```cpp
        /// class MyOutOfScopeClass
        /// {
        /// #include "MyInScopeFile.h"
        /// };
        /// ```
        /// 
        /// In practice, however, this should never really happen.
        /// If it does, we probably need to adjust how translation works anyway.
        /// In theory if there's weird partial files like this, we probably don't want them to be in-scope anyway (the outer file should be in-scope too.)
        ///
        /// Note that a file included by another normally does not become a child of any cursors in the including file.
        /// This is because the preprocessor essentially flattens the entire include tree.
        /// </remarks>
        [Conditional("DEBUG")]
        private void AssertAllChildrenOutOfScope(Cursor cursor)
        {
            foreach (Cursor child in cursor.CursorChildren)
            {
                // Attributes can get implicitly added from other files by Clang so we don't try to check them
                if (child is not Attr)
                {
                    CXFile childFile = child.Extent.Start.GetFileLocation();

                    // Cursors which libclang doesn't handle might not have location information
                    if (childFile.Handle != IntPtr.Zero && FileHandleToTranslatedFileLookup.TryGetValue(childFile.Handle, out TranslatedFile? translatedFile))
                    { Debug.Assert(!translatedFile.WasInScope, "All children of a cursor that is out of scope must be out of scope."); }
                }

                AssertAllChildrenOutOfScope(child);
            }
        }

        private unsafe void ProcessMacro(PathogenMacroInformation* macroInfo)
        {
            if (!Options.IncludeUndefinedMacros && macroInfo->WasUndefined)
            { return; }

            CXFile fileHandle = macroInfo->Location.GetFileLocation();
            bool isSynthesized = fileHandle.Handle == IntPtr.Zero;

            if (!Options.IncludeSynthesizedMacros && isSynthesized)
            { return; }

            TranslatedFile file = GetTranslatedFile(fileHandle);

            if (!isSynthesized && !Options.IncludeMacrosDefinedOutOfScope && !file.WasInScope)
            { return; }

            MacrosBuilder.Add(new TranslatedMacro(file, macroInfo));
        }

        private unsafe void ProcessImplicitlyInstantiatedSpecializedTemplates()
        {
            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            static void EnumerateClassTemplateSpecialization(PathogenTemplateSpecializationKind kind, CXCursor cursor, void* userData)
            {
                if (kind != PathogenTemplateSpecializationKind.ImplicitInstantiation)
                { return; }

                //TODO: We should probably handle this more elegantly.
                // Invalid implicitly-specialized templates can occur if one of the type parameters isn't defined.
                // In theory we should emit something similar to TranslatedUndefinedRecord, but we need to make sure we only do that if the specialization is invalid for this reason.
                // An error will be emitted when this happens, so we aren't totally setting up things for confusing errors.
                // TemplateTests.TemplateInstantiationErrorsAreEmitted can trigger this code path.
                if (cursor.IsInvalidDeclaration)
                { return; }

                GCHandle thisHandle = GCHandle.FromIntPtr((IntPtr)userData);
                TranslationUnitParser parser = Unsafe.As<TranslationUnitParser>(thisHandle.Target)!;

                Cursor rawDecl = parser.FindCursor(cursor);
                ClassTemplateSpecializationDecl specialization = (ClassTemplateSpecializationDecl)rawDecl;
                TranslatedFile file = parser.GetTranslatedFile(rawDecl);

                //TODO: Should implicitly instantiated templates be implicitly in-scope if they are referenced in scope?
                // If so, we need to change how we enumerate the templates. (The ClangSharp.Pathogen extension simply enumerates _every_ referenced template present anywhere.)
                if (!file.WasInScope)
                { return; }

                parser.DeclarationsBuilder.Add(new TranslatedTemplateSpecialization(parser, file, specialization));
            }

            GCHandle thisHandle = default;
            try
            {
                thisHandle = GCHandle.Alloc(this);
                PathogenExtensions.pathogen_EnumerateAllSpecializedClassTemplates(TranslationUnit.Handle, &EnumerateClassTemplateSpecialization, (void*)GCHandle.ToIntPtr(thisHandle));
            }
            finally
            {
                if (thisHandle.IsAllocated)
                { thisHandle.Free(); }
            }
        }

        private bool ResultsFetched = false;
        internal void GetResults
        (
            out ImmutableArray<TranslatedFile> files,
            out ImmutableArray<TranslationDiagnostic> parsingDiagnostics,
            out ImmutableList<TranslatedDeclaration> declarations,
            out ImmutableArray<TranslatedMacro> macros
        )
        {
            if (!ParsingComplete)
            { throw new InvalidOperationException("This method can only be called once parsing is complete."); }

            if (ResultsFetched)
            { throw new InvalidOperationException("Results can be fetched only once."); }

            ResultsFetched = true;
            files = FilesBuilder.MoveToImmutableSafe();
            parsingDiagnostics = ParsingDiagnosticsBuilder.MoveToImmutableSafe();
            declarations = DeclarationsBuilder.ToImmutable();

            // Internally within ClangSharp.Pathogen, macros are enumerated from a hash map
            // As such the apparent order seems nonsensical so we sort them for the sake of determinism
            MacrosBuilder.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            macros = MacrosBuilder.MoveToImmutableSafe();
        }
    }
}
