using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Biohazrd
{
    public sealed partial class TranslatedLibraryBuilder
    {
        private readonly List<string> CommandLineArguments = new();
        private readonly List<SourceFile> Files = new();

        public TranslationOptions Options { get; set; } = new();

        public void AddFile(SourceFile sourceFile)
        {
            Debug.Assert(Path.IsPathFullyQualified(sourceFile.FilePath), "File paths should always be fully qualified.");

            // We can't index files with quotes in their paths (possible on Linux) because they can't be included
            if (sourceFile.IndexDirectly && sourceFile.FilePath.Contains('"'))
            { throw new ArgumentException("Files marked to be indexed must not have quotes in thier path.", nameof(sourceFile)); }

            // If provided, contents must be either a string or bytes but not both
            if (sourceFile.Contents is not null && !sourceFile.ContentBytes.IsEmpty)
            { throw new ArgumentException($"Files must not have both {nameof(sourceFile.Contents)} and {nameof(sourceFile.ContentBytes)}.", nameof(sourceFile)); }

            // If a file is virtual, it must have contents
            if (sourceFile.IsVirtual && !sourceFile.HasContents)
            { throw new ArgumentException("Virtual files must have contents.", nameof(sourceFile)); }

            // If the file has contents, eagerly encode it as UTF8 bytes so any encoding errors happen sooner rather than later
            if (sourceFile.Contents is not null)
            {
                sourceFile = sourceFile with
                {
                    Contents = null,
                    ContentBytes = Encoding.UTF8.GetBytes(sourceFile.Contents)
                };
            }

            Files.Add(sourceFile);
        }

        public void AddFile(string filePath)
        {
            if (!File.Exists(filePath))
            { throw new FileNotFoundException("The specified file does not exist.", filePath); }

            // Ensure the path is absolute
            // (That way if the working directory changes, we still have a valid path.)
            // (This also normalizes the path.)
            filePath = Path.GetFullPath(filePath);

            AddFile(new SourceFile(filePath));
        }

        public void AddFiles(IEnumerable<string> filePaths)
        {
            if (filePaths is ICollection<string> filePathsList)
            { Files.Capacity += filePathsList.Count; }

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
            __HACK__Stl1300Workaround stl1300Workaround = __HACK__Stl1300Workaround.Instance;

            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // Create an index file in-memory which includes all of the files to be processed
            // We want to process all files as a single translation unit because it makes it much easier to reason about relationships between declarations in individual files.
            //
            // This does assume that all input files can be included in the same translation unit and that they use `#pragma once` or equivalent header guards.
            // Since this is typical of well-formed C++ libraries, it should be fine.
            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            SourceFile indexFile;
            {
                StringBuilder indexFileCodeTextBuilder = new StringBuilder();

                foreach (SourceFile file in Files)
                {
                    if (file.IndexDirectly)
                    { indexFileCodeTextBuilder.AppendLine($"#include \"{file.FilePath}\""); }
                }

                // According to documentation this file must already exist on the filesystem, but that doesn't actually seem to be true for
                // the primary file or any files included by absolute path.
                // (However, Clang will not be able to find any files included by relative path if they don't actually exist.)
                // https://clang.llvm.org/doxygen/structCXUnsavedFile.html#aa8bf5d4351628ee8502b517421e8b418
                // In fact, we intentionally use a file name that's illegal (on Windows) so it's unlikely we conflict with any real files.
                indexFile = new SourceFile($"<>BiohazrdIndexFile.cpp")
                {
                    IsInScope = false,
                    IndexDirectly = false,
                    ContentBytes = Encoding.UTF8.GetBytes(indexFileCodeTextBuilder.ToString())
                };
            }

            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // Create the translation unit
            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            TranslationUnitAndIndex? translationUnitAndIndex = null;
            TranslationUnit? translationUnit = null;
            {
                List<CXUnsavedFile> unsavedFiles = new(stl1300Workaround.ShouldBeApplied ? 2 : 1);
                List<MemoryHandle> memoryHandles = new(1);

                CXIndex clangIndex = default;
                CXTranslationUnit translationUnitHandle = default;

                try
                {
                    //---------------------------------------------------------------------------------
                    // Create the unsaved files list
                    //---------------------------------------------------------------------------------
                    // LLVM will internally copy the buffers we pass to it, so pinning these only for the duration of the index creation is fine here.
                    // https://github.com/llvm/llvm-project/blob/llvmorg-10.0.0/clang/tools/libclang/CIndex.cpp#L3497

                    // Add the index file
                    unsavedFiles.Add(indexFile.CreateUnsavedFile(memoryHandles));

                    // Add the STL1300 workaround if needed
                    if (stl1300Workaround.ShouldBeApplied)
                    { unsavedFiles.Add(stl1300Workaround.UnsavedFile); }

                    // Add user-specified memory files
                    foreach (SourceFile file in Files)
                    {
                        if (file.HasContents)
                        { unsavedFiles.Add(file.CreateUnsavedFile(memoryHandles)); }
                    }

                    //---------------------------------------------------------------------------------
                    // Create the translation unit
                    //---------------------------------------------------------------------------------
                    // Do not enable CXTranslationUnit_IncludeAttributedTypes without resolving https://github.com/InfectedLibraries/Biohazrd/issues/130
                    const CXTranslationUnit_Flags translationUnitFlags = 0;

                    // Allocate the libclang Index
                    clangIndex = CXIndex.Create();

                    CXErrorCode translationUnitStatus = CXTranslationUnit.TryParse
                    (
                        clangIndex,
                        indexFile.FilePath,
                        CollectionsMarshal.AsSpan(CommandLineArguments),
                        CollectionsMarshal.AsSpan(unsavedFiles),
                        translationUnitFlags,
                        out translationUnitHandle
                    );

                    // In the event parsing fails, we throw an exception
                    // This generally never happens since Clang usually emits diagnostics in a healthy manner.
                    // libclang uses the status code to report things like internal programming errors or invalid arguments.
                    if (translationUnitStatus != CXErrorCode.CXError_Success)
                    { throw new InvalidOperationException($"Failed to parse the Biohazrd index file due to a fatal Clang error {translationUnitStatus}."); }

                    // Create the translation unit
                    translationUnit = TranslationUnit.GetOrCreate(translationUnitHandle);

                    // Create the index/translation unit pair
                    translationUnitAndIndex = new TranslationUnitAndIndex(clangIndex, translationUnit);
                }
                finally
                {
                    // Free all of the memory handles
                    foreach (MemoryHandle memoryHandle in memoryHandles)
                    { memoryHandle.Dispose(); }

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
            TranslationUnitParser processor = new(Files, Options, translationUnit);
            ImmutableArray<TranslatedFile> files;
            ImmutableArray<TranslationDiagnostic> parsingDiagnostics;
            ImmutableList<TranslatedDeclaration> declarations;
            ImmutableArray<TranslatedMacro> macros;
            processor.GetResults(out files, out parsingDiagnostics, out declarations, out macros);

            if (stl1300Workaround.Diagnostics.Length > 0)
            { parsingDiagnostics = stl1300Workaround.Diagnostics.AddRange(parsingDiagnostics); }

            // Create the library
            return new TranslatedLibrary(translationUnitAndIndex, files, parsingDiagnostics, declarations, macros);
        }
    }
}
