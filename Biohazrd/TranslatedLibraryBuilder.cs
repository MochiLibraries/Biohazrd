using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Biohazrd
{
    public sealed partial class TranslatedLibraryBuilder
    {
        private readonly List<string> CommandLineArguments = new List<string>();
        private readonly List<string> FilePaths = new List<string>();

        public void AddFile(string filePath)
        {
            if (!File.Exists(filePath))
            { throw new FileNotFoundException("The specified file does not exist.", filePath); }

            // Ensure the path is absolute
            // (That way if the working directory changes, we still have a valid path.)
            // (This also normalizes the path.)
            filePath = Path.GetFullPath(filePath);

            FilePaths.Add(filePath);
        }

        public void AddFiles(IEnumerable<string> filePaths)
        {
            foreach (string filePath in filePaths)
            { AddFile(filePath); }
        }

        public void AddFiles(params string[] filePaths)
            => AddFiles((IEnumerable<string>)filePaths);

        public void AddCommandLineArgument(string commandLineArgument)
            => CommandLineArguments.Add(commandLineArgument);

        public void AddCommandLineArguments(IEnumerable<string> commandLineArguments)
            => CommandLineArguments.AddRange(commandLineArguments);

        public void AddCommandLineArguments(params string[] commandLineArguments)
            => AddCommandLineArguments((IEnumerable<string>)commandLineArguments);

        public unsafe TranslatedLibrary Create()
        {
            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // Create an index file in-memory which includes all of the files to be processed
            // We want to process all files as a single translation unit because it makes it much easier to reason about relationships between declarations in individual files.
            //
            // This does assume that all input files can be included in the same translation unit and that they use `#pragma once` or equivalent header guards.
            // Since this is typical of well-formed C++ libraries, it should be fine.
            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            string indexFileName;
            string indexCodeText;
            {
                // According to documentation this file must already exist on the filesystem, but that doesn't actually seem to be true.
                // https://clang.llvm.org/doxygen/structCXUnsavedFile.html#aa8bf5d4351628ee8502b517421e8b418
                // In fact, we intentionally use a file name that's illegal (on Windows) so it's unlikely we conflict with any real files.
                indexFileName = $"<>{nameof(TranslatedLibrary)}IndexFile.cpp";

                StringBuilder indexFileCodeTextBuilder = new StringBuilder();

                foreach (string filePath in FilePaths)
                { indexFileCodeTextBuilder.AppendLine($"#include \"{filePath}\""); }

                indexCodeText = indexFileCodeTextBuilder.ToString();
                //indexFileCodeTextBytes = Encoding.UTF8.GetBytes(indexFileCodeTextBuilder.ToString());
            }

            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // Create the translation unit
            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            TranslationUnitAndIndex? translationUnitAndIndex = null;
            TranslationUnit? translationUnit = null;
            {
                CXIndex clangIndex = default;
                CXTranslationUnit translationUnitHandle = default;

                try
                {
                    const CXTranslationUnit_Flags translationUnitFlags = CXTranslationUnit_Flags.CXTranslationUnit_IncludeAttributedTypes;

                    // Allocate the libclang Index
                    clangIndex = CXIndex.Create();

                    // LLVM will internally copy the buffers we pass to it, so pinning them is fine here.
                    // https://github.com/llvm/llvm-project/blob/llvmorg-10.0.0/clang/tools/libclang/CIndex.cpp#L3497
                    byte[] indexFileCodeTextBytes = Encoding.UTF8.GetBytes(indexCodeText);
                    fixed (byte* indexFileNamePtr = Encoding.UTF8.GetBytesNullTerminated(indexFileName))
                    fixed (byte* indexCodeTextPtr = indexFileCodeTextBytes)
                    {
                        Span<CXUnsavedFile> unsavedFiles = stackalloc CXUnsavedFile[1];
                        unsavedFiles[0] = new CXUnsavedFile()
                        {
                            Filename = (sbyte*)indexFileNamePtr,
                            Contents = (sbyte*)indexCodeTextPtr,
                            Length = (UIntPtr)indexFileCodeTextBytes.Length
                        };

                        CXErrorCode translationUnitStatus = CXTranslationUnit.TryParse
                        (
                            clangIndex,
                            indexFileName,
                            CollectionsMarshal.AsSpan(CommandLineArguments),
                            unsavedFiles,
                            translationUnitFlags,
                            out translationUnitHandle
                        );

                        // In the event parsing fails, we throw an exception
                        // This generally never happens since Clang usually emits diagnostics in a healthy manner.
                        // libclang uses the status code to report things like internal programming errors or invalid arguments.
                        if (translationUnitStatus != CXErrorCode.CXError_Success)
                        { throw new InvalidOperationException($"Failed to parse the Biohazrd index file due to a fatal Clang error {translationUnitStatus}."); }
                    }

                    // Create the translation unit
                    translationUnit = TranslationUnit.GetOrCreate(translationUnitHandle);

                    // Create the index/translation unit pair
                    translationUnitAndIndex = new TranslationUnitAndIndex(clangIndex, translationUnit);
                }
                finally
                {
                    // If we failed to create the translation unit/index pair, make sure to dispose of the index/translation unit
                    if (translationUnitAndIndex is null)
                    {
                        if (clangIndex.Handle != default)
                        { clangIndex.Dispose(); }

                        if (translationUnit is not null)
                        { translationUnit.Dispose(); }
                        else if (translationUnitHandle.Handle != default)
                        { translationUnitHandle.Dispose(); }
                    }
                }
            }

            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // Process the translation unit
            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            TranslationUnitParser processor = new(FilePaths, translationUnit);
            ImmutableArray<TranslatedFile> files;
            ImmutableArray<TranslationDiagnostic> parsingDiagnostics;
            ImmutableList<TranslatedDeclaration> declarations;
            processor.GetResults(out files, out parsingDiagnostics, out declarations);

            // Create the library
            return new TranslatedLibrary(translationUnitAndIndex, files, parsingDiagnostics, declarations);
        }
    }
}
