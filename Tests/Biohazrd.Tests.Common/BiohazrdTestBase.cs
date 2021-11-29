using Biohazrd.OutputGeneration;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace Biohazrd.Tests.Common
{
    public abstract class BiohazrdTestBase
    {
        protected virtual TranslationOptions? DefaultTranslationOptions => null;
        protected virtual string? DefaultTargetTriple => null;

        /// <param name="targetTriple">https://clang.llvm.org/docs/CrossCompilation.html#target-triple</param>
        protected TranslatedLibraryBuilder CreateLibraryBuilder(string cppCode, string? targetTriple = null, TranslationOptions? options = null)
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = cppCode
            });

            targetTriple ??= DefaultTargetTriple;
            if (targetTriple is not null)
            { builder.AddCommandLineArgument($"--target={targetTriple}"); }

            if (options is not null)
            { builder.Options = options; }
            else if (DefaultTranslationOptions is TranslationOptions defaultOptions)
            { builder.Options = defaultOptions; }

            return builder;
        }

        /// <param name="targetTriple">https://clang.llvm.org/docs/CrossCompilation.html#target-triple</param>
        protected TranslatedLibrary CreateLibrary(string cppCode, string? targetTriple = null, TranslationOptions? options = null)
        {
            TranslatedLibrary library = CreateLibraryBuilder(cppCode, targetTriple, options).Create();
            Assert.Empty(library.ParsingDiagnostics.Where(d => d.IsError));
            return library;
        }

        private volatile int NextOutputFolderId = 0;
        protected OutputSession CreateOutputSession([CallerMemberName] string testName = null!)
        {
            if (testName is null)
            { throw new ArgumentNullException(nameof(testName)); }

            int folderId = Interlocked.Increment(ref NextOutputFolderId) - 1;
            string outputDirectoryName = $"Output_{GetType().Name}_{testName}";

            // The folder ID is used to ensure a unique output directory for any tests which have multiple output sessions
            if (folderId > 0)
            { outputDirectoryName += $"_{folderId}"; }

            string outputDirectoryPath = Path.Combine(Environment.CurrentDirectory, outputDirectoryName);

            if (Directory.Exists(outputDirectoryPath))
            {
                //TODO: This basically manually re-implements the same functionality in OutputSession.ProcessAndUpdateFileLog
                // It'd be nice if OutputSession provided a Clean method for doing this.
                string fileLogPath = Path.Combine(outputDirectoryPath, "FilesWritten.txt");
                if (!File.Exists(fileLogPath))
                { throw new InvalidOperationException($"Temporary output session directory '{outputDirectoryPath}' doesn't look like an old output session! Refusing to clear it out for safety, please delete it manually."); }

                string[] loggedFilePaths = File.ReadAllLines(fileLogPath);
                foreach (string loggedFilePath in loggedFilePaths)
                { File.Delete(Path.Combine(outputDirectoryPath, loggedFilePath)); }

                File.Delete(fileLogPath);

                if (Directory.EnumerateFiles(outputDirectoryPath).Any())
                { throw new InvalidOperationException($"Temporary output session directory '{outputDirectoryPath}' has unlogged files. Refusing to clear it out for safety, please delete it manually."); }

                Directory.Delete(outputDirectoryPath, recursive: true);
            }

            return new OutputSession()
            {
                // For tests we generally don't want this behavior
                AutoRenameConflictingFiles = false,
                BaseOutputDirectory = outputDirectoryPath,
                // Disable the generated file header by default
                GeneratedFileHeader = null,
                // Require tests to opt-out of conservative logging if they don't want it
                ConservativeFileLogging = true
            };
        }
    }
}
