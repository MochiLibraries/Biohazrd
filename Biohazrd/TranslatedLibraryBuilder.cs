using ClangSharp;
using ClangSharp.Interop;
using ClangSharp.Pathogen;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Biohazrd
{
    public sealed partial class TranslatedLibraryBuilder
    {
        private readonly List<string> CommandLineArguments = new();
        private readonly List<SourceFileInternal> Files = new();

        public TranslationOptions Options { get; set; } = new();

        public TranslatedLibraryBuilder()
        {
            // On non-Windows platforms we need to provide the Clang resource directory.
            // This specifies the version copied to our output directory by ClangSharp.Pathogen.Runtime.
            // (One Windows the same files come from the UCRT instead.)
            // See https://github.com/InfectedLibraries/Biohazrd/issues/201 for more details
            string resourceDirectoryPath = Path.Combine(AppContext.BaseDirectory, "clang-resources");
            if (!OperatingSystem.IsWindows())
            {
                if (!Directory.Exists(resourceDirectoryPath) || !File.Exists(Path.Combine(resourceDirectoryPath, "include", "stddef.h")))
                { throw new DirectoryNotFoundException("Clang resources directory not found."); }
                
                AddCommandLineArguments("-resource-dir", resourceDirectoryPath);
            }
        }

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

        private void InitializeClang()
        {
            if (Environment.GetEnvironmentVariable("BIOHAZRD_CUSTOM_LIBCLANG_PATHOGEN_RUNTIME") is string customNativeRuntime)
            {
                LibClangSharpResolver.OverrideNativeRuntime(customNativeRuntime);
            }

            LibClangSharpResolver.VerifyResolverWasUsed();
        }

        public unsafe TranslatedLibrary Create()
        {
            InitializeClang();
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
                    //
                    // Note that it is intentional that this is done even when TranslationOptions.EnableTemplateSupport is disabled because without doing so causes some Clang APIs to behave
                    // erratically because Clang doesn't normally access them for implicitly-instantiated templates which didn't need to be instantiated.
                    //
                    // This probably matters less now because TranslatedFunction will implicitly instantiate templates when checking if it is callable, but we want to do it here nice and early
                    // to avoid the translation unit mutation issues mentioned above.
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
            return new TranslatedLibrary(translationUnitAndIndex, processor.CodeGeneratorPool, files, parsingDiagnostics, declarations, macros);
        }

        /// <summary>Creates a constant evaluator for evaluating macros and arbitrary C++ expressions.</summary>
        /// <remarks>The constant evaluator has a significant overhead (internally it has to reparse the entirity of the C++ library) so don't create it unless you plan to actually use it.</remarks>
        public TranslatedLibraryConstantEvaluator CreateConstantEvaluator()
        {
            InitializeClang();

            return new TranslatedLibraryConstantEvaluator
            (
                CreateIndexFile(),
                Files.ToImmutableArray(),
                CreateUnsavedFilesList(indexFile: null), // The constant evaluator needs to be responsible for the index file so it can keep it from being collected.
                CollectionsMarshal.AsSpan(CommandLineArguments)
            );
        }
    }
}
