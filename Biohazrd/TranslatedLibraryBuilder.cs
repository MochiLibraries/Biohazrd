using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
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


        private static bool HaveInstalledLibClangResolver = false;
        private static object HaveInstalledLibClangResolverLock = new();
        private static volatile bool OurResolverWasUsedForClang = false;
        /// <summary>Installs Biohazrd's libclang resolver for ClangSharp.</summary>
        /// <remarks>
        /// You do not typically need to call this method, but if you are using ClangSharp directly in your code before your first call to <see cref="Create"/>,
        /// you must call this method before any usage of ClangSharp. If you fail to do so, <see cref="Create"/> will throw an exception and/or Biohazrd may experience issues.
        ///
        /// For details on the issue this method is working around, see https://github.com/InfectedLibraries/llvm-project/issues/2#issuecomment-712897834
        /// </remarks>
        public static unsafe void __HACK__InstallLibClangDllWorkaround()
        {
            // This is a workaround to avoid loading two different libclang DLLs, and can be removed once https://github.com/InfectedLibraries/llvm-project/issues/2 is fixed.
            // If we don't do this, weird things can happen in some scenarios.
            // (For example, pathogen_ComputerConstantValue can return garbage for floats because LLVM uses compares pointers to statically allocated memory to differentiate various float storages.)
            // In theory this could be in ClangSharp.Pathogen instead, but we'd have to call it here anyway since we need to ensure this happens before ClangSharp is used.
            static IntPtr LibClangResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
            {
                if (libraryName == "libclang")
                {
                    OurResolverWasUsedForClang = true;
                    return NativeLibrary.Load("libclang-pathogen.dll", typeof(ClangSharp.Pathogen.ClangSharpExtensions).Assembly, null);
                }

                return IntPtr.Zero;
            }

            lock (HaveInstalledLibClangResolverLock)
            {
                if (!HaveInstalledLibClangResolver)
                {
                    clang.ResolveLibrary += LibClangResolver;
                    HaveInstalledLibClangResolver = true;

                    // Calling createIndex causes the runtime to resolve the export for it.
                    // Since it is basically the starting point for actually using libclang, we can use this to determine if ClangSharp was used before our resolver was installed.
                    // We can't use something like clang.getVersion because the runtime resolves the DLL separately for each function and it might not have been called.
                    void* index = null;
                    try
                    {
                        index = clang.createIndex(0, 0);

                        if (!OurResolverWasUsedForClang)
                        {
                            throw new InvalidOperationException
                            (
                                "ClangSharp was initialized before we were able to install our resolver! " +
                                $"Manually call {typeof(TranslatedLibraryBuilder).FullName}.{nameof(__HACK__InstallLibClangDllWorkaround)} at the start of Main to resolve this issue."
                            );
                        }
                    }
                    finally
                    {
                        // This needs to happen _after_ the check or the loading of disposeIndex might trigger our check since someone may have created and index but is yet to dispose of it.
                        if (index is not null)
                        { clang.disposeIndex(index); }
                    }
                }
            }
        }

        public unsafe TranslatedLibrary Create()
        {
            __HACK__InstallLibClangDllWorkaround();

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
