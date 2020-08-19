using ClangSharp;
using ClangSharp.Interop;
using ClangSharp.Pathogen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ClangType = ClangSharp.Type;

namespace Biohazrd
{
    public sealed class TranslatedLibrary : IDisposable
    {
        private readonly CXIndex Index;
        private readonly TranslationUnit TranslationUnit;

        private readonly Dictionary<string, string> AllFilePaths; // path => path, this is a dictionary so we can use the canonical name provided to this library rather than whatever Clang gives us.
        private readonly HashSet<string> UnusedFilePaths;
        private List<TranslatedFile> Files = new List<TranslatedFile>();
        private readonly Dictionary<IntPtr, TranslatedFile> FileHandleToTranslatedFileLookup = new Dictionary<IntPtr, TranslatedFile>();
        private readonly HashSet<IntPtr> KnownOutOfScopeFileHandles = new HashSet<IntPtr>();

        private readonly Dictionary<Decl, TranslatedDeclaration> DeclarationLookup = new Dictionary<Decl, TranslatedDeclaration>();

        /// <summary>True if any file in this library contains diagnostics with <see cref="TranslationDiagnostic.IsError"/> of true.</summary>
        public bool HasErrors { get; private set; }

        internal unsafe TranslatedLibrary(IEnumerable<string> clangCommandLineArguments, IEnumerable<string> filePaths)
        {
            Index = CXIndex.Create(displayDiagnostics: true);
            // We treat file paths are case-insensitive
            // This is technically only valid on case-insensitive systems, but in practice it's uncommon for two files to have the same case-insensitive name even on systems which support it.
            // (Especially for code which may be checked into source control and cloned on other operating systems.)
            // When https://github.com/dotnet/runtime/issues/14321 is realized, we should consider normalizing the paths to actual case instead.
            UnusedFilePaths = new HashSet<string>(filePaths, StringComparer.OrdinalIgnoreCase);
            AllFilePaths = new Dictionary<string, string>(UnusedFilePaths.Count, StringComparer.OrdinalIgnoreCase);

            foreach (string filePath in filePaths)
            { AllFilePaths.Add(filePath, filePath); }

            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // Create an index file which includes all of the files to be processed
            // We want to process all files as a single translation unit so that we can reason about relationships between declarations in individual files.
            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // Clang uses UTF8
            Encoding encoding = Encoding.UTF8;

            // According to documentation this file must already exist on the filesystem, but that doesn't actually seem to be true.
            // https://clang.llvm.org/doxygen/structCXUnsavedFile.html#aa8bf5d4351628ee8502b517421e8b418
            // In fact, we intentionally use a file name that's illegal (on Windows) so it's unlikely we conflict with any real files.
            string indexFileName = $"<>{nameof(TranslatedLibrary)}IndexFile.cpp";
            byte[] indexFileNameBytes = encoding.GetBytesNullTerminated(indexFileName);

            StringBuilder indexFileCodeTextBuilder = new StringBuilder();

            foreach (string filePath in filePaths)
            { indexFileCodeTextBuilder.AppendLine($"#include \"{filePath}\""); }

            byte[] indexFileCodeTextBytes = Encoding.UTF8.GetBytes(indexFileCodeTextBuilder.ToString());

            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // Create the translation unit
            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            CXTranslationUnit translationUnitHandle;
            CXErrorCode translationUnitStatus;

            CXTranslationUnit_Flags translationUnitFlags = CXTranslationUnit_Flags.CXTranslationUnit_IncludeAttributedTypes;

            // LLVM will internally copy the buffers we pass to it, so pinning them is fine here.
            // https://github.com/llvm/llvm-project/blob/llvmorg-10.0.0/clang/tools/libclang/CIndex.cpp#L3497
            fixed (byte* indexFileNamePtr = indexFileNameBytes)
            fixed (byte* indexCodeTextPtr = indexFileCodeTextBytes)
            {
                Span<CXUnsavedFile> unsavedFiles = stackalloc CXUnsavedFile[1];
                unsavedFiles[0] = new CXUnsavedFile()
                {
                    Filename = (sbyte*)indexFileNamePtr,
                    Contents = (sbyte*)indexCodeTextPtr,
                    Length = (UIntPtr)indexFileCodeTextBytes.Length
                };

                translationUnitStatus = CXTranslationUnit.TryParse(Index, indexFileName, clangCommandLineArguments.ToArray(), unsavedFiles, translationUnitFlags, out translationUnitHandle);
            }

            // Handle total parsing failure
            // This generally never happens since Clang usually emits diagnostics in a healthy manner.
            // libclang uses the status code to report things like internal programming errors or invalid arguments.
            if (translationUnitStatus != CXErrorCode.CXError_Success)
            {
                Diagnostic(Severity.Fatal, new SourceLocation(indexFileName), $"Failed to parse index file due to Clang error {translationUnitStatus}.");
                return;
            }

            // Emit diagnostics
            try
            {
                if (translationUnitHandle.NumDiagnostics != 0)
                {
                    for (uint i = 0; i < translationUnitHandle.NumDiagnostics; i++)
                    {
                        using CXDiagnostic diagnostic = translationUnitHandle.GetDiagnostic(i);
                        Diagnostic(new TranslationDiagnostic(diagnostic));
                    }

                    if (HasErrors)
                    {
                        Diagnostic(Severity.Fatal, SourceLocation.Null, "Aborting translation due to previous errors.");
                        translationUnitHandle.Dispose();
                        return;
                    }
                }
            }
            catch
            {
                translationUnitHandle.Dispose();
                throw;
            }

            // Create the translation unit and process the cursors within it
            TranslationUnit = TranslationUnit.GetOrCreate(translationUnitHandle);

            foreach (Cursor cursor in TranslationUnit.TranslationUnitDecl.CursorChildren)
            { ProcessCursor(cursor); }
        }

        [Conditional("DEBUG")]
        private void AssertAllChildrenOutOfScope(Cursor cursor)
        {
            foreach (Cursor child in cursor.CursorChildren)
            {
                // Attributes can get implicitly added from other files by Clang.
                if (!(child is Attr))
                {
                    CXFile childFile = child.Extent.Start.GetFileLocation();

                    // Cursors which libclang doesn't handle might not have location information
                    if (childFile.Handle != IntPtr.Zero)
                    { Debug.Assert(KnownOutOfScopeFileHandles.Contains(childFile.Handle), "All children of a cursor that is out of scope must be out of scope."); }
                }

                AssertAllChildrenOutOfScope(child);
            }
        }

        private void ProcessCursor(Cursor cursor)
        {
            // Ignore cursors from system headers
            // (System headers in Clang are headers that come from specific files or have a special pragma. This does not mean "ignore #include <...>".)
            if (cursor.Extent.Start.IsInSystemHeader || cursor.Extent.End.IsInSystemHeader)
            { return; }

            // Figure out what file this cursor comes from
            CXFile cursorFile = cursor.Extent.Start.GetFileLocation();
            Debug.Assert(cursor.Extent.End.GetFileLocation().Handle == cursorFile.Handle, "The start and end file handles of a cursor should match.");

            // If this file is known to be out of scope, skip the cursor
            if (KnownOutOfScopeFileHandles.Contains(cursorFile.Handle))
            {
                // In theory this assert can fail if an in-scope file is included in the middle of another cursor.
                // For example:
                // class MyOutOfScopeClass
                // {
                // #include "MyInScopeFile.h"
                // };
                // In practice, however, this should never really happen.
                // If it does, we probably need to adjust how translation works anyway.
                // In theory if there's weird partial files like this, we probably don't want them to be in-scope anyway.
                //
                // Note that a file included by another normally does not become a child of any cursors in the including file.
                // This is because the preprocessor essentially flattens the entire include tree.
                AssertAllChildrenOutOfScope(cursor);
                return;
            }


            // Look up the TranslatedFile associated with this file handle or determine if we need to create one
            TranslatedFile translatedFile;
            if (!FileHandleToTranslatedFileLookup.TryGetValue(cursorFile.Handle, out translatedFile))
            {
                // Get the name of the file
                string fileName = cursorFile.TryGetRealPathName().ToString();

                if (String.IsNullOrEmpty(fileName))
                { fileName = cursorFile.Name.ToString(); }

                // Make sure we have the full path and that it's normalized.
                fileName = Path.GetFullPath(fileName);

                // See if we know about this file and it is unused.
                // If it isn't, this file is out of scope
                if (UnusedFilePaths.Remove(fileName) == false)
                {
                    // This assert could fail if in theory if Clang gave us two different `CXFile`s with the same file name but different handles.
                    Debug.Assert(!AllFilePaths.ContainsKey(fileName), "At this point the file shouldn't be one of our files at all.");

                    KnownOutOfScopeFileHandles.Add(cursorFile.Handle);
                    AssertAllChildrenOutOfScope(cursor);
                    return;
                }

                // Create a new TranslatedFile to represent this file
                // Note that we look up the canonical name from AllFilePaths instead of using fileName directly
                // This ensures the file casing associated wtih TranslatedFile is consistent with what was passed to TranslatedLibraryBuilder.
                translatedFile = new TranslatedFile(this, cursorFile.Handle, AllFilePaths[fileName]);
                FileHandleToTranslatedFileLookup.Add(cursorFile.Handle, translatedFile);
                Files.Add(translatedFile);
            }

            // Tell the file to process the cursor
            translatedFile.ProcessCursor(translatedFile, cursor);
        }

        internal void AddDeclarationAssociation(Decl declaration, TranslatedDeclaration translatedDeclaration)
        {
            Debug.Assert(declaration.TranslationUnit == TranslationUnit, "The declaration must belong to this translation unit.");

            if (DeclarationLookup.TryGetValue(declaration, out TranslatedDeclaration otherDeclaration))
            {
                Diagnostic
                (
                    Severity.Error,
                    declaration,
                    $"More than one translation corresponds to {declaration.CursorKindDetailed()} '{declaration.Spelling}' (Newest: {translatedDeclaration.GetType().Name}, Other: {otherDeclaration.GetType().Name})"
                );
                return;
            }

            DeclarationLookup.Add(declaration, translatedDeclaration);
        }

        internal void RemoveDeclarationAssociation(Decl declaration, TranslatedDeclaration translatedDeclaration)
        {
            Debug.Assert(declaration.TranslationUnit == TranslationUnit, "The declaration must belong to this translation unit.");

            if (DeclarationLookup.TryGetValue(declaration, out TranslatedDeclaration otherDeclaration) && !ReferenceEquals(otherDeclaration, translatedDeclaration))
            {
                Diagnostic
                (
                    Severity.Error,
                    declaration,
                    $"Tried to remove association between {declaration.CursorKindDetailed()} '{declaration.Spelling}' and a {translatedDeclaration.GetType().Name}, but it's associated with a (different) {otherDeclaration.GetType().Name}"
                );
                return;
            }

            bool removed = DeclarationLookup.Remove(declaration, out otherDeclaration);
            Debug.Assert(removed && ReferenceEquals(translatedDeclaration, otherDeclaration));
        }

        internal TranslatedDeclaration TryFindTranslation(Decl declaration)
        {
            Debug.Assert(declaration.TranslationUnit == TranslationUnit, "The declaration must belong to this translation unit.");

            if (DeclarationLookup.TryGetValue(declaration, out TranslatedDeclaration ret))
            { return ret; }

            return null;
        }

        internal Cursor FindCursor(CXCursor cursorHandle)
        {
            if (cursorHandle.IsNull)
            {
                Diagnostic(Severity.Warning, SourceLocation.Null, $"Someone tried to get the Cursor for a null handle.");
                return null;
            }

            return TranslationUnit.GetOrCreate(cursorHandle);
        }

        internal ClangType FindType(CXType typeHandle)
        {
            if (typeHandle.kind == CXTypeKind.CXType_Invalid)
            {
                Diagnostic(Severity.Warning, SourceLocation.Null, $"Someone tried to get the Type for an invalid type handle.");
                return null;
            }

            return TranslationUnit.GetOrCreate(typeHandle);
        }

        public void Validate()
        {
            foreach (string filePath in UnusedFilePaths)
            { Diagnostic(Severity.Note, new SourceLocation(filePath), "Input file did not appear in Clang's cursor tree."); }

            foreach (TranslatedFile file in Files)
            { file.Validate(); }
        }

        public void ApplyTransformation(Func<TranslatedDeclaration, TranslationTransformation> transformationFactoryMethod)
            => ApplyTransformation(new SimpleTranslationTransformationFactory(transformationFactoryMethod));

        public void ApplyTransformation(TranslationTransformationFactory transformationFactory)
        {
            List<TranslationTransformation> transformations = new List<TranslationTransformation>();
            EnumerateTransformations(transformationFactory, transformations);

            foreach (TranslationTransformation transformation in transformations)
            {
                Console.WriteLine($"Applying transformation: {transformation}");
                transformation.Apply();
            }
        }

        private void EnumerateTransformations(TranslationTransformationFactory transformationFactory, List<TranslationTransformation> transformations)
        {
            foreach (TranslatedFile file in Files)
            { EnumerateTransformations(transformationFactory, transformations, file); }
        }

        private void EnumerateTransformations(TranslationTransformationFactory transformationFactory, List<TranslationTransformation> transformations, IDeclarationContainer container)
        {
            foreach (TranslatedDeclaration declaration in container)
            {
                TranslationTransformation transformation = transformationFactory.CreateInternal(declaration);

                if (transformation != null)
                { transformations.Add(transformation); }

                if (declaration is IDeclarationContainer nestedContainer)
                { EnumerateTransformations(transformationFactory, transformations, nestedContainer); }
            }
        }

        public void Translate(LibraryTranslationMode mode)
        {
            switch (mode)
            {
                case LibraryTranslationMode.OneFilePerType:
                {
                    foreach (TranslatedFile file in Files)
                    { file.Translate(); }
                }
                break;
                case LibraryTranslationMode.OneFilePerInputFile:
                {
                    foreach (TranslatedFile file in Files)
                    {
                        if (file.IsEmptyTranslation)
                        { continue; }

                        using CodeWriter writer = new CodeWriter();
                        file.Translate(writer);
                        string outputFileName = Path.GetFileNameWithoutExtension(file.FilePath) + ".cs";
                        writer.WriteOut(outputFileName);
                    }
                }
                break;
                case LibraryTranslationMode.OneFile:
                {
                    using CodeWriter writer = new CodeWriter();

                    foreach (TranslatedFile file in Files)
                    { file.Translate(writer); }

                    writer.WriteOut("TranslatedLibrary.cs");
                }
                break;
                default:
                    throw new ArgumentException("The specified mode is invalid.", nameof(mode));
            }
        }

        private readonly UnnamedNamer UnnamedNamer = new UnnamedNamer();
        internal string GetNameForUnnamed(string category)
            => UnnamedNamer.GetName(category);

        internal void Diagnostic(Severity severity, SourceLocation location, string message)
            => Diagnostic(new TranslationDiagnostic(location, severity, message));

        internal void Diagnostic(Severity severity, Cursor associatedCursor, string message)
            => Diagnostic(severity, new SourceLocation(associatedCursor.Extent.Start), message);

        internal void Diagnostic(Severity severity, CXCursor associatedCursor, string message)
            => Diagnostic(severity, new SourceLocation(associatedCursor.Extent.Start), message);

        internal void Diagnostic(in TranslationDiagnostic diagnostic)
        {
            if (diagnostic.IsError)
            { HasErrors = true; }

            TextWriter output;
            ConsoleColor oldForegroundColor = Console.ForegroundColor;
            ConsoleColor oldBackgroundColor = Console.BackgroundColor;

            try
            {
                switch (diagnostic.Severity)
                {
                    case Severity.Ignored:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        output = Console.Out;
                        break;
                    case Severity.Note:
                        output = Console.Out;
                        break;
                    case Severity.Warning:
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        output = Console.Error;
                        break;
                    case Severity.Error:
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        output = Console.Error;
                        break;
                    case Severity.Fatal:
                    default:
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.BackgroundColor = ConsoleColor.DarkRed;
                        output = Console.Error;
                        break;
                }

                if (!diagnostic.Location.IsNull)
                {
                    string fileName = Path.GetFileName(diagnostic.Location.SourceFile);
                    if (diagnostic.Location.Line != 0)
                    { output.WriteLine($"{diagnostic.Severity} at {fileName}:{diagnostic.Location.Line}: {diagnostic.Message}"); }
                    else
                    { output.WriteLine($"{diagnostic.Severity} at {fileName}: {diagnostic.Message}"); }
                }
                else
                { output.WriteLine($"{diagnostic.Severity}: {diagnostic.Message}"); }
            }
            finally
            {
                Console.BackgroundColor = oldBackgroundColor;
                Console.ForegroundColor = oldForegroundColor;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool isDisposing)
        {
            if (isDisposing)
            { TranslationUnit?.Dispose(); }

            Index.Dispose();
        }

        ~TranslatedLibrary()
        {
            // Dispose order matters for the unmanaged resources indirectly managed by this class, so it's not ideal to allow it to be garbage collected.
            // As such, we complain if we're allowed to be collected.
            Console.Error.WriteLine("TranslatedLibrary must be disposed of explicitly!");
            Debugger.Break();
            Dispose(false);
        }
    }
}
