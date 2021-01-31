using ClangSharp;
using ClangSharp.Interop;
using ClangSharp.Pathogen;
using System;
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
        private readonly List<SourceFileInternal> Files = new();

        public TranslationOptions Options { get; set; } = new();

        public void AddFile(SourceFile sourceFile)
        {
            Debug.Assert(Path.IsPathFullyQualified(sourceFile.FilePath), "File paths should always be fully qualified.");

            // We can't index files with quotes in their paths (possible on Linux) because they can't be included
            if (sourceFile.IndexDirectly && sourceFile.FilePath.Contains('"'))
            { throw new ArgumentException("Files marked to be indexed must not have quotes in thier path.", nameof(sourceFile)); }

            // If a file is virtual, it must have contents
            if (sourceFile.IsVirtual && sourceFile.Contents is null)
            { throw new ArgumentException("Virtual files must have contents.", nameof(sourceFile)); }

            Files.Add(new SourceFileInternal(sourceFile));
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

        /// <summary>Creates the Biohazrd index file</summary>
        /// <remarks>
        /// Creates an index file in-memory which includes all of the files to be processed
        /// We want to process all files as a single translation unit because it makes it much easier to reason about relationships between declarations in individual files.
        ///
        /// This does assume that all input files can be included in the same translation unit and that they use `#pragma once` or equivalent header guards.
        /// Since this is typical of well-formed C++ libraries, it should be fine.
        /// </remarks>
        private SourceFile CreateIndexFile()
        {
            StringBuilder indexFileCodeTextBuilder = new StringBuilder();

            foreach (SourceFileInternal file in Files)
            {
                if (file.IndexDirectly)
                { indexFileCodeTextBuilder.AppendLine($"#include \"{file.FilePath}\""); }
            }

            // According to documentation this file must already exist on the filesystem, but that doesn't actually seem to be true for
            // the primary file or any files included by absolute path.
            // (However, Clang will not be able to find any files included by relative path if they don't actually exist.)
            // https://clang.llvm.org/doxygen/structCXUnsavedFile.html#aa8bf5d4351628ee8502b517421e8b418
            // In fact, we intentionally use a file name that's illegal (on Windows) so it's unlikely we conflict with any real files.
            return new SourceFile($"<>BiohazrdIndexFile.cpp")
            {
                IsInScope = false,
                IndexDirectly = false,
                Contents = indexFileCodeTextBuilder.ToString()
            };
        }

        /// <summary>Creates an <see cref="CXUnsavedFile"/> listing to pass to Clang for parsing this library</summary>
        /// <remarks>
        /// The first file will always be the index file.
        ///
        /// Make sure to keep the <paramref name="indexFile"/> reference alive as long as the resulting list is alive to avoid the file's buffers from being garbage collected.
        ///
        /// If <paramref name="indexFile"/> is null, the index file entry will be defaulted and must be populated by the caller.
        /// </remarks>
        private List<CXUnsavedFile> CreateUnsavedFilesList(SourceFileInternal? indexFile)
        {
            __HACK__Stl1300Workaround stl1300Workaround = __HACK__Stl1300Workaround.Instance;
            List<CXUnsavedFile> result = new(stl1300Workaround.ShouldBeApplied ? 2 : 1);

            // Add the index file
            result.Add(indexFile is not null ? indexFile.UnsavedFile : default(CXUnsavedFile));

            // Add the STL1300 workaround if needed
            if (stl1300Workaround.ShouldBeApplied)
            { result.Add(stl1300Workaround.UnsavedFile); }

            // Add user-specified in-memory files
            foreach (SourceFileInternal file in Files)
            {
                if (file.HasUnsavedFile)
                { result.Add(file.UnsavedFile); }
            }

            return result;
        }

        public unsafe TranslatedLibrary Create()
        {
            __HACK__InstallLibClangDllWorkaround();

            ImmutableArray<TranslationDiagnostic>.Builder miscDiagnostics = ImmutableArray.CreateBuilder<TranslationDiagnostic>();

            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            // Create the translation unit
            //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
            TranslationUnitAndIndex? translationUnitAndIndex = null;
            TranslationUnit? translationUnit = null;
            {
                CXIndex clangIndex = default;
                CXTranslationUnit translationUnitHandle = default;
                SourceFileInternal? indexFile = null;

                try
                {
                    //---------------------------------------------------------------------------------
                    // Create the translation unit
                    //---------------------------------------------------------------------------------
                    // Do not enable CXTranslationUnit_IncludeAttributedTypes without resolving https://github.com/InfectedLibraries/Biohazrd/issues/130
                    const CXTranslationUnit_Flags translationUnitFlags = 0;

                    // Allocate the libclang Index
                    clangIndex = CXIndex.Create();

                    // Create unsaved files
                    indexFile = new SourceFileInternal(CreateIndexFile());
                    List<CXUnsavedFile> unsavedFiles = CreateUnsavedFilesList(indexFile);

                    CXErrorCode translationUnitStatus = CXTranslationUnit.TryParse
                    (
                        clangIndex,
                        indexFile.FilePath,
                        CollectionsMarshal.AsSpan(CommandLineArguments),
                        CollectionsMarshal.AsSpan(unsavedFiles),
                        translationUnitFlags,
                        out translationUnitHandle
                    );

                    // Ensure the index file sticks around until parsing is completed
                    GC.KeepAlive(indexFile);

                    // In the event parsing fails, we throw an exception
                    // This generally never happens since Clang usually emits diagnostics in a healthy manner.
                    // libclang uses the status code to report things like internal programming errors or invalid arguments.
                    if (translationUnitStatus != CXErrorCode.CXError_Success)
                    { throw new InvalidOperationException($"Failed to parse the Biohazrd index file due to a fatal Clang error {translationUnitStatus}."); }

                    // Instantiate all fully-sepcialized templates in the translation unit
                    // Clang instantiates fully specialized templates lazily, which means when they're only used in some contexts it will treat them as defined.
                    // (For instance, a specialization only used for a parameter's type in a declarared function is never actually implicitly defined.)
                    // As such, we do this to fully define all implicitly-defined fully specified templates across the entire translation unit.
                    // We want to do this earlier rather than later since it ends up mutating the translation unit, which ClangSharp tends to dislike since it assumes it's immutable.
                    // See https://github.com/InfectedLibraries/Biohazrd/issues/153 for details.
                    {
                        PathogenTemplateInstantiationMetrics metrics = PathogenExtensions.pathogen_InstantiateAllFullySpecializedClassTemplates(translationUnitHandle);

                        if (metrics.SuccessfulInstantiationsCount > 0)
                        { miscDiagnostics.Add(Severity.Note, $"Successfully late-instantiated {metrics.SuccessfulInstantiationsCount} template specialization{(metrics.SuccessfulInstantiationsCount == 1 ? "" : "s")}."); }

                        if (metrics.FailedInstantiationsCount > 0)
                        { miscDiagnostics.Add(Severity.Warning, $"Failed to late-instantiate {metrics.FailedInstantiationsCount} template specialization{(metrics.FailedInstantiationsCount == 1 ? "" : "s")}."); }
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
                        if (translationUnit is not null)
                        { translationUnit.Dispose(); }
                        else if (translationUnitHandle.Handle != default)
                        { translationUnitHandle.Dispose(); }

                        if (clangIndex.Handle != default)
                        { clangIndex.Dispose(); }
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

            // Prepend misc diagnostics if we have any
            if (miscDiagnostics.Count > 0)
            {
                miscDiagnostics.AddRange(parsingDiagnostics);
                parsingDiagnostics = miscDiagnostics.MoveToImmutableSafe();
            }

            __HACK__Stl1300Workaround stl1300Workaround = __HACK__Stl1300Workaround.Instance;
            if (stl1300Workaround.Diagnostics.Length > 0)
            { parsingDiagnostics = stl1300Workaround.Diagnostics.AddRange(parsingDiagnostics); }

            // Create the library
            return new TranslatedLibrary(translationUnitAndIndex, files, parsingDiagnostics, declarations, macros);
        }

        /// <summary>Creates a constant evaluator for evaluating macros and arbitrary C++ expressions.</summary>
        /// <remarks>The constant evaluator has a significant overhead (internally it has to reparse the entirity of the C++ library) so don't create it unless you plan to actually use it.</remarks>
        public TranslatedLibraryConstantEvaluator CreateConstantEvaluator()
            => new TranslatedLibraryConstantEvaluator
            (
                CreateIndexFile(),
                Files.ToImmutableArray(),
                CreateUnsavedFilesList(indexFile: null), // The constant evaluator needs to be responsible for the index file so it can keep it from being collected.
                CollectionsMarshal.AsSpan(CommandLineArguments)
            );
    }
}
