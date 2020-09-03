using Biohazrd;
using Biohazrd.Transformation.Common;
using ClangSharp.Interop;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace ClangSharpTest2020
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            DoTest();
            Console.WriteLine();
            Console.WriteLine("Done.");
            Console.ReadLine();
        }

        private static void DoTest()
        {
            string[] includeDirs =
            {
                @"C:\Scratch\PhysX\physx\install\vc15win64\PhysX\include\",
                @"C:\Scratch\PhysX\physx\install\vc15win64\PxShared\include\"
            };

            List<string> _clangCommandLineArgs = new List<string>()
            {
                "-D_DEBUG",
                "--language=c++",
                "--std=c++17",
                "-Wno-pragma-once-outside-header", // Since we are parsing headers, this warning will be irrelevant.
                "-Wno-return-type-c-linkage", // PxGetFoundation triggers this. There's code to suppress it, but it's only triggered when building for Clang on Linux.
                "-Wno-microsoft-include", // This triggers on a few includes for some reason.
                //"--target=x86_64-pc-linux",
                //"--target=i386-pc-win32",
            };

            foreach (string includeDir in includeDirs)
            { _clangCommandLineArgs.Add($"-I{includeDir}"); }

            string[] clangCommandLineArgs = _clangCommandLineArgs.ToArray();

            CXIndex index = CXIndex.Create(displayDiagnostics: true);

            List<string> files = new List<string>();

            const string outputDirectory = "OutputPhysX";
            HashSet<string> allowedFiles = new HashSet<string>()
            {
            };

            HashSet<string> skippedFiles = new HashSet<string>()
            {
                "PxUnixIntrinsics.h", // Not relevant on Windows
            };

            foreach (string includeDir in includeDirs)
            {
                foreach (string headerFile in Directory.EnumerateFiles(includeDir, "*.h", SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileName(headerFile);

                    if (skippedFiles.Contains(fileName))
                    { continue; }

                    files.Add(headerFile);
                }
            }

            // Ensure all files are absolute paths since we're about to change directories
            for (int i = 0; i < files.Count; i++)
            {
                if (!Path.IsPathRooted(files[i]))
                { files[i] = Path.GetFullPath(files[i]); }
            }

            if (Directory.Exists(outputDirectory))
            {
                foreach (string file in Directory.EnumerateFiles(outputDirectory))
                { File.Delete(file); }
            }

            using WorkingDirectoryScope _ = new WorkingDirectoryScope(outputDirectory);

            // Copy the file to the output directory for easier inspection.
            foreach (string file in files)
            { File.Copy(file, Path.GetFileName(file)); }

            // Create the library
            TranslatedLibraryBuilder libraryBuilder = new TranslatedLibraryBuilder();
            libraryBuilder.AddCommandLineArguments(clangCommandLineArgs);
            libraryBuilder.AddFiles(files);

            TranslatedLibrary library = libraryBuilder.Create();

            // Perform validation
            Console.WriteLine("==============================================================================");
            Console.WriteLine("Performing pre-translation validation...");
            Console.WriteLine("==============================================================================");
            library.Validate();

            // Apply transformations
            Console.WriteLine("==============================================================================");
            Console.WriteLine("Performing library-specific transformations...");
            Console.WriteLine("==============================================================================");
            library = new ConstOverloadRenameTransformation().Transform(library);
            library = new PhysXRemovePaddingFieldsTransformation().Transform(library);
            library = new PhysXEnumTransformation().Transform(library);
            library = new PhysXFlagsEnumTransformation(library).Transform(library);
            library = new MakeEvereythingPublicTransformation().Transform(library);

            using (var generateModuleDefinition = new GenerateModuleDefinitionTransformation(@"C:\Scratch\PhysX\physx\PhysXPathogen.def", files))
            {
                library.ApplyTransformation(generateModuleDefinition.Factory);
            }

            // Emit the translation
            Console.WriteLine("==============================================================================");
            Console.WriteLine("Performing translation...");
            Console.WriteLine("==============================================================================");
            library.Translate(LibraryTranslationMode.OneFilePerInputFile);

            // Build csproj
            Console.WriteLine("==============================================================================");
            Console.WriteLine("Building generated C# code...");
            Console.WriteLine("==============================================================================");
            {
                CSharpBuildHelper build = new CSharpBuildHelper();
                foreach (string generatedFile in Directory.EnumerateFiles(".", "*.cs", SearchOption.AllDirectories))
                { build.AddFile(generatedFile); }

                int errorCount = 0;
                int warningCount = 0;

                ImmutableArray<Diagnostic> diagnostics = build.CompileAndEmit("Output.dll");

                foreach (Diagnostic diagnostic in diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Hidden)
                    { continue; }

                    switch (diagnostic.Severity)
                    {
                        case DiagnosticSeverity.Warning:
                            warningCount++;
                            break;
                        case DiagnosticSeverity.Error:
                            errorCount++;
                            break;
                    }

                    WriteDiagnostic(diagnostic);
                }

                Console.WriteLine($"========== C# build {(errorCount > 0 ? "failed" : "succeeded")}: {errorCount} error(s), {warningCount} warning(s) ==========");
            }
        }
        private static void WriteDiagnostic(Diagnostic diagnostic)
        {
            TextWriter output;
            ConsoleColor oldForegroundColor = Console.ForegroundColor;
            ConsoleColor oldBackgroundColor = Console.BackgroundColor;

            try
            {
                switch (diagnostic.Severity)
                {
                    case DiagnosticSeverity.Hidden:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        output = Console.Out;
                        break;
                    case DiagnosticSeverity.Info:
                        output = Console.Out;
                        break;
                    case DiagnosticSeverity.Warning:
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        output = Console.Error;
                        break;
                    case DiagnosticSeverity.Error:
                    default:
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        output = Console.Error;
                        break;
                }

                output.WriteLine(diagnostic);
            }
            finally
            {
                Console.BackgroundColor = oldBackgroundColor;
                Console.ForegroundColor = oldForegroundColor;
            }
        }
    }
}
