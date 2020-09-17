using Biohazrd;
using Biohazrd.CSharp;
using Biohazrd.OutputGeneration;
using Biohazrd.Transformation.Common;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

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

            List<string> files = new List<string>();

            const string outputDirectory = "OutputPhysX";

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

            // Delete any existing files in the output
            if (Directory.Exists(outputDirectory))
            {
                foreach (string file in Directory.EnumerateFiles(outputDirectory))
                { File.Delete(file); }
            }

            // Start output session
            using OutputSession outputSession = new OutputSession()
            {
                AutoRenameConflictingFiles = true,
                BaseOutputDirectory = outputDirectory
            };

            // Copy the file to the output directory for easier inspection.
            foreach (string file in files)
            { outputSession.CopyFile(file); }

            // Create the library
            TranslatedLibraryBuilder libraryBuilder = new TranslatedLibraryBuilder();
            libraryBuilder.AddCommandLineArguments(clangCommandLineArgs);
            libraryBuilder.AddFiles(files);

            TranslatedLibrary library = libraryBuilder.Create();

            // Apply transformations
            Console.WriteLine("==============================================================================");
            Console.WriteLine("Performing library-specific transformations...");
            Console.WriteLine("==============================================================================");

            BrokenDeclarationExtractor brokenDeclarationExtractor = new();
            library = brokenDeclarationExtractor.Transform(library);

            library = new RemoveBadPhysXDeclarationsTransformation().Transform(library);
            library = new PhysXRemovePaddingFieldsTransformation().Transform(library);
            library = new PhysXEnumTransformation().Transform(library);
            library = new PhysXFlagsEnumTransformation().Transform(library);

            library = new AddBaseVTableAliasTransformation().Transform(library);
            library = new ConstOverloadRenameTransformation().Transform(library);
            library = new MakeEvereythingPublicTransformation().Transform(library);

            library = new RemoveRemainingTypedefsTransformation().Transform(library);
            library = new TypeReductionTransformation().Transform(library);
            library = new CSharpBuiltinTypeTransformation().Transform(library);
            library = new KludgeUnknownClangTypesIntoBuiltinTypesTransformation(emitErrorOnFail: true).Transform(library);
            library = new DeduplicateNamesTransformation().Transform(library);
            library = new MoveLooseDeclarationsIntoTypesTransformation().Transform(library); //TODO: Ideally this happens sooner, but it can interfere with type transformations currently.

            // Perform validation
            Console.WriteLine("==============================================================================");
            Console.WriteLine("Performing post-translation validation...");
            Console.WriteLine("==============================================================================");
            library = new CSharpTranslationVerifier().Transform(library);

            // Remove final broken declarations
            library = brokenDeclarationExtractor.Transform(library);

            // Generate module definition
            ModuleDefinitionGenerator.Generate(outputSession, @"C:\Scratch\PhysX\physx\PhysXPathogen.def", library, "PhysXPathogen_64");
            InlineReferenceFileGenerator.Generate(outputSession, @"C:\Scratch\PhysX\physx\PhysXPathogen.cpp", library);

            // Emit the translation
            Console.WriteLine("==============================================================================");
            Console.WriteLine("Performing translation...");
            Console.WriteLine("==============================================================================");
            ImmutableArray<TranslationDiagnostic> generationDiagnostics = CSharpLibraryGenerator.Generate
            (
                CSharpGenerationOptions.Default,
                outputSession,
                library,
                LibraryTranslationMode.OneFilePerInputFile
            );

            // Write out diagnostics log
            using StreamWriter diagnosticsOutput = outputSession.Open<StreamWriter>("Diagnostics.log");

            void OutputDiagnostic(in TranslationDiagnostic diagnostic)
            {
                WriteDiagnosticToConsole(diagnostic);
                WriteDiagnosticToWriter(diagnostic, diagnosticsOutput);
            }

            diagnosticsOutput.WriteLine("==============================================================================");
            diagnosticsOutput.WriteLine("Translation Diagnostics");
            diagnosticsOutput.WriteLine("==============================================================================");

            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (declaration.Diagnostics.Length > 0)
                {
                    diagnosticsOutput.WriteLine($"--------------- {declaration.GetType().Name} {declaration.Name} ---------------");

                    foreach (TranslationDiagnostic diagnostic in declaration.Diagnostics)
                    { OutputDiagnostic(diagnostic); }
                }
            }

            if (brokenDeclarationExtractor.BrokenDeclarations.Length > 0)
            {
                diagnosticsOutput.WriteLine("==============================================================================");
                diagnosticsOutput.WriteLine("Broken Declarations");
                diagnosticsOutput.WriteLine("==============================================================================");

                foreach (TranslatedDeclaration declaration in brokenDeclarationExtractor.BrokenDeclarations)
                {
                    diagnosticsOutput.WriteLine($"=============== {declaration.GetType().Name} {declaration.Name} ===============");

                    foreach (TranslationDiagnostic diagnostic in declaration.Diagnostics)
                    { OutputDiagnostic(diagnostic); }
                }
            }

            diagnosticsOutput.WriteLine("==============================================================================");
            diagnosticsOutput.WriteLine("Generation Diagnostics");
            diagnosticsOutput.WriteLine("==============================================================================");

            if (generationDiagnostics.Length == 0)
            { diagnosticsOutput.WriteLine("Generation completed successfully."); }
            else
            {
                foreach (TranslationDiagnostic diagnostic in generationDiagnostics)
                { OutputDiagnostic(diagnostic); }
            }

            // Close the output session to unlock all of the output files so they can be read for building
            outputSession.Dispose();

            // Build csproj
            using StreamWriter cSharpDiagnosticsLog = new(Path.Combine(outputDirectory, "Diagnostics_Roslyn.log"));
            Console.WriteLine("==============================================================================");
            Console.WriteLine("Building generated C# code...");
            Console.WriteLine("==============================================================================");
            {
                CSharpBuildHelper build = new CSharpBuildHelper();
                foreach (string generatedFile in outputSession.FilesWritten.Where(s => s.EndsWith(".cs")))
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

                    string diagnosticString;

                    if (!diagnostic.Location.IsInSource)
                    { diagnosticString = diagnostic.ToString(); }
                    else
                    {
                        FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
                        diagnosticString = $"{Path.GetFileName(span.Path)}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): ";
                        diagnosticString += $"{diagnostic.Severity.ToString().ToLowerInvariant()} {diagnostic.Id}: {diagnostic.GetMessage()}";
                    }

                    WriteDiagnosticToConsole(diagnostic.Severity, diagnosticString);
                    cSharpDiagnosticsLog.WriteLine(diagnosticString);
                }

                string summaryLine = $"========== C# build {(errorCount > 0 ? "failed" : "succeeded")}: {errorCount} error(s), {warningCount} warning(s) ==========";
                Console.WriteLine(summaryLine);
                cSharpDiagnosticsLog.WriteLine(summaryLine);
            }
        }

        private static void WriteDiagnosticToConsole(in TranslationDiagnostic diagnostic)
        {
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

                WriteDiagnosticToWriter(diagnostic, output);
            }
            finally
            {
                Console.BackgroundColor = oldBackgroundColor;
                Console.ForegroundColor = oldForegroundColor;
            }
        }

        private static void WriteDiagnosticToWriter(in TranslationDiagnostic diagnostic, TextWriter output)
        {
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

        private static void WriteDiagnosticToConsole(DiagnosticSeverity severity, string message)
        {
            TextWriter output;
            ConsoleColor oldForegroundColor = Console.ForegroundColor;
            ConsoleColor oldBackgroundColor = Console.BackgroundColor;

            try
            {
                switch (severity)
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

                output.WriteLine(message);
            }
            finally
            {
                Console.BackgroundColor = oldBackgroundColor;
                Console.ForegroundColor = oldForegroundColor;
            }
        }
    }
}
