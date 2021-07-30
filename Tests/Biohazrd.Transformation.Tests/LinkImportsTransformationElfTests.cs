using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Biohazrd.Transformation.Tests
{
    public sealed class LinkImportsTransformationElfTests : BiohazrdTestBase
    {
        // We use this test to make these tests optional on Windows (they'll only run if a valid Clang installation is available
        public sealed class NeedsLinuxOrClangFactAttribute : FactAttribute
        {
            public NeedsLinuxOrClangFactAttribute()
            {
                // Nothing to do if we're already being skipped
                if (base.Skip is not null)
                { return; }

                // On non-Windows platforms we always run these tests, we expect Clang to be available on the system PATH
                if (!OperatingSystem.IsWindows())
                { return; }

                // Always run these tests for CI
                if (Environment.GetEnvironmentVariable("ContinuousIntegrationBuild")?.Equals("true", StringComparison.InvariantCultureIgnoreCase) ?? false)
                { return; }

                // Skip this test if Clang is not avialable
                switch (LlvmTools.IsClangAvailable())
                {
                    case FileNotFoundException:
                        Skip = "This test requires Clang to be available via the system PATH or via Visual Studio.";
                        break;
                    case Exception exception:
                        Skip = $"This test requires an operational version of Clang: {exception.Message}";
                        break;
                }
            }
        }

        /// <summary>Creates an ELF shared library with the given name and exports.</summary>
        /// <param name="transformation">An optional transformation to automatically add the library to.</param>
        /// <param name="libName">The name of the library without any file extension.</param>
        /// <param name="useCPlusPlus">If true, the symbols will have C++ name manglings applied.</param>
        /// <param name="exports">One or more symbols to export.</param>
        /// <remarks>
        /// Symbols can be prefixed with a <c>~</c> to indicate they are weak exports.
        ///
        /// Symbols can be suffixed with an optional segment to mark them as object symbols instead of functions.
        /// The following segments are supported: <c>.data</c>, <c>.rodata</c>, and <c>.bss</c>
        ///
        /// Symbols can be suffixed with a <c>!</c> followed by an optional visibility. Valid visibilities are those supported by the GCC visibility attribute.
        /// </remarks>
        private void CreateSharedLibrary(LinkImportsTransformation? transformation, string libName, bool useCPlusPlus, params string[] exports)
        {
            libName += ".so";

            // Create dummy C definitions to create the desired symbols
            StringBuilder exportCode = new();
            foreach (string export in exports)
            {
                if (String.IsNullOrEmpty(export))
                { throw new ArgumentException("Export list contains a null/empty export.", nameof(exports)); }

                ReadOnlySpan<char> exportName = export;

                // Check if weak
                bool isWeak = false;
                if (exportName[0] == '~')
                {
                    isWeak = true;
                    exportName = exportName.Slice(1);
                }

                // Get the visibility
                int visibilityDelimiter = exportName.LastIndexOf('!');
                ReadOnlySpan<char> visibility = "";
                if (visibilityDelimiter != -1)
                {
                    visibility = exportName.Slice(visibilityDelimiter + 1);
                    exportName = exportName.Slice(0, visibilityDelimiter);

                    // Don't let quotes get all the way to the source since they're invalid.
                    if (visibility.IndexOf('"') != -1)
                    { throw new ArgumentException($"Export '{export}' visibility contains invalid symbols.", nameof(exports)); }
                }

                // Determine the prefix/suffix based on the segment
                string prefix = "void ";
                string suffix = "() { }";
                int segmentDelimiter = exportName.LastIndexOf('.');
                if (segmentDelimiter != -1)
                {
                    ReadOnlySpan<char> segment = exportName.Slice(segmentDelimiter + 1);
                    exportName = exportName.Slice(0, segmentDelimiter);

                    if (segment.SequenceEqual("text"))
                    {
                        // Functions are the default, nothing to do
                    }
                    else if (segment.SequenceEqual("data"))
                    {
                        prefix = "int ";
                        suffix = " = 100;";
                    }
                    else if (segment.SequenceEqual("rodata"))
                    {
                        prefix = "const int ";
                        suffix = " = 200;";
                    }
                    else if (segment.SequenceEqual("bss"))
                    {
                        prefix = "int ";
                        suffix = ";";
                    }
                    else
                    { throw new ArgumentException($"Export '{export}' program segment '{segment.ToString()}' is unrecognized.", nameof(exports)); }
                }

                // Emit the definition
                if (isWeak)
                { exportCode.Append("__attribute__((weak)) "); }

                if (!visibility.IsEmpty)
                {
                    exportCode.Append("__attribute__((visibility (\"");
                    exportCode.Append(visibility);
                    exportCode.Append("\"))) ");
                }

                exportCode.Append(prefix);
                exportCode.Append(exportName);
                exportCode.Append(suffix);
                exportCode.AppendLine();
            }

            // Start Clang
            string clangPath = LlvmTools.GetClangPath();
            using Process clang = new()
            {
                StartInfo = new ProcessStartInfo(clangPath, $"--target=x86_64-pc-linux --language={(useCPlusPlus ? "c++" : "c")} --shared --output={libName} -")
                {
                    RedirectStandardInput = true
                }
            };

            // Add extra arguments required for Windows
            // These could be safely added on Linux too, but:
            // A) lld might not be installed
            // B) Not including standard libraries is extremely atypical, we don't want to deviate that far from normalcy.
            //    (We need to do it on Windows simply because we don't have glibc and such there.)
            if (OperatingSystem.IsWindows())
            { clang.StartInfo.Arguments = $"-fuse-ld=lld --no-standard-libraries {clang.StartInfo.Arguments}"; }

            clang.Start();

            // Write our code to Clang to create the shared library
            using (StreamWriter writer = clang.StandardInput)
            { writer.Write(exportCode); }

            // Wait for Clang to finish up
            clang.WaitForExit();

            if (clang.ExitCode != 0)
            { throw new Exception($"Clang failed with exit code {clang.ExitCode} while creating the shared library."); }

            // We use a relative directory here so that:
            // A) We test that only the file name is used
            // B) We can tell when verbose import information was tracked
            transformation?.AddLibrary($"./{libName}");
        }

        /// <summary>Creates an ELF shared library with the given name and exports.</summary>
        /// <param name="transformation">An optional transformation to automatically add the library to.</param>
        /// <param name="libName">The name of the library without any file extension.</param>
        /// <param name="exports">One or more symbols to export.</param>
        /// <remarks>
        /// Symbols can be prefixed with a <c>~</c> to indicate they are weak exports.
        ///
        /// Symbols can be suffixed with an optional segment to mark them as object symbols instead of functions.
        /// The following segments are supported: <c>.data</c>, <c>.rodata</c>, and <c>.bss</c>
        ///
        /// Symbols can be suffixed with a <c>!</c> followed by an optional visibility. Valid visibilities are those supported by the GCC visibility attribute.
        /// </remarks>
        private void CreateSharedLibrary(LinkImportsTransformation? transformation, string libName, params string[] exports)
            => CreateSharedLibrary(transformation, libName, useCPlusPlus: false, exports);

        [NeedsLinuxOrClangFact]
        public void NormalSymbolTest()
        {
            LinkImportsTransformation transformation = new();
            CreateSharedLibrary(transformation, nameof(NormalSymbolTest), "TestFunction");
            TranslatedLibrary library = CreateLibrary(@"extern ""C"" void TestFunction();");
            library = transformation.Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.Equal($"{nameof(NormalSymbolTest)}.so", function.DllFileName);
            Assert.Equal("TestFunction", function.MangledName);
            Assert.Empty(function.Diagnostics);
        }

        [NeedsLinuxOrClangFact]
        public void NormalSymbolTest_GlobalVariable()
        {
            LinkImportsTransformation transformation = new();
            CreateSharedLibrary(transformation, nameof(NormalSymbolTest_GlobalVariable), "TestGlobal.data");
            TranslatedLibrary library = CreateLibrary(@"extern ""C"" int TestGlobal;");
            library = transformation.Transform(library);

            TranslatedStaticField staticField = library.FindDeclaration<TranslatedStaticField>("TestGlobal");
            Assert.Equal($"{nameof(NormalSymbolTest_GlobalVariable)}.so", staticField.DllFileName);
            Assert.Equal("TestGlobal", staticField.MangledName);
            Assert.Empty(staticField.Diagnostics);
        }

        [NeedsLinuxOrClangFact]
        public void MangledCppSymbolTest()
        {
            LinkImportsTransformation transformation = new();
            const string testFunctionMangled = "_Z12TestFunctionv";
            CreateSharedLibrary(transformation, nameof(MangledCppSymbolTest), useCPlusPlus: true, "TestFunction");
            TranslatedLibrary library = CreateLibrary(@"void TestFunction();", "x86_64-pc-linux");
            library = transformation.Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.Equal($"{nameof(MangledCppSymbolTest)}.so", function.DllFileName);
            Assert.Equal(testFunctionMangled, function.MangledName);
        }

        [NeedsLinuxOrClangFact]
        public void MultipleSymbolTest()
        {
            LinkImportsTransformation transformation = new();
            CreateSharedLibrary(transformation, nameof(MultipleSymbolTest), "TestFunction", "AnotherFunction");
            TranslatedLibrary library = CreateLibrary
            (@"
extern ""C"" void TestFunction();
extern ""C"" void AnotherFunction();
"
            );
            library = transformation.Transform(library);

            TranslatedFunction testFunction = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.Equal($"{nameof(MultipleSymbolTest)}.so", testFunction.DllFileName);
            Assert.Equal("TestFunction", testFunction.MangledName);

            TranslatedFunction anotherFunction = library.FindDeclaration<TranslatedFunction>("AnotherFunction");
            Assert.Equal($"{nameof(MultipleSymbolTest)}.so", anotherFunction.DllFileName);
            Assert.Equal("AnotherFunction", anotherFunction.MangledName);
        }

        [NeedsLinuxOrClangFact]
        public void MultipleLibraries()
        {
            LinkImportsTransformation transformation = new();
            CreateSharedLibrary(transformation, $"{nameof(MultipleLibraries)}_0", "TestFunction");
            CreateSharedLibrary(transformation, $"{nameof(MultipleLibraries)}_1", "AnotherFunction");
            TranslatedLibrary library = CreateLibrary
            (@"
extern ""C"" void TestFunction();
extern ""C"" void AnotherFunction();
"
            );
            library = transformation.Transform(library);

            TranslatedFunction testFunction = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.Equal($"{nameof(MultipleLibraries)}_0.so", testFunction.DllFileName);
            Assert.Equal("TestFunction", testFunction.MangledName);

            TranslatedFunction anotherFunction = library.FindDeclaration<TranslatedFunction>("AnotherFunction");
            Assert.Equal($"{nameof(MultipleLibraries)}_1.so", anotherFunction.DllFileName);
            Assert.Equal("AnotherFunction", anotherFunction.MangledName);
        }

        [NeedsLinuxOrClangFact]
        public void AmbiguousSymbolResolvesToFirst()
        {
            LinkImportsTransformation transformation = new();
            CreateSharedLibrary(transformation, $"{nameof(AmbiguousSymbolResolvesToFirst)}_0", "TestFunction");
            CreateSharedLibrary(transformation, $"{nameof(AmbiguousSymbolResolvesToFirst)}_1", "TestFunction");
            TranslatedLibrary library = CreateLibrary(@"extern ""C"" void TestFunction();");
            library = transformation.Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.Equal($"{nameof(AmbiguousSymbolResolvesToFirst)}_0.so", function.DllFileName);
            Assert.Equal("TestFunction", function.MangledName);
        }

        [NeedsLinuxOrClangFact]
        public void WarnOnAmbiguousSymbols_False()
        {
            LinkImportsTransformation transformation = new()
            {
                WarnOnAmbiguousSymbols = false
            };

            CreateSharedLibrary(transformation, $"{nameof(WarnOnAmbiguousSymbols_True)}_0", "TestFunction");
            CreateSharedLibrary(transformation, $"{nameof(WarnOnAmbiguousSymbols_True)}_1", "TestFunction");
            TranslatedLibrary library = CreateLibrary(@"extern ""C"" void TestFunction();");
            library = transformation.Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.Equal($"{nameof(WarnOnAmbiguousSymbols_True)}_0.so", function.DllFileName);
            Assert.Equal("TestFunction", function.MangledName);
            Assert.Empty(function.Diagnostics);
        }

        [NeedsLinuxOrClangFact]
        public void WarnOnAmbiguousSymbols_True()
        {
            LinkImportsTransformation transformation = new()
            {
                WarnOnAmbiguousSymbols = true
            };

            CreateSharedLibrary(transformation, $"{nameof(WarnOnAmbiguousSymbols_True)}_0", "TestFunction");
            CreateSharedLibrary(transformation, $"{nameof(WarnOnAmbiguousSymbols_True)}_1", "TestFunction");
            TranslatedLibrary library = CreateLibrary(@"extern ""C"" void TestFunction();");
            library = transformation.Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.Equal($"{nameof(WarnOnAmbiguousSymbols_True)}_0.so", function.DllFileName);
            Assert.Equal("TestFunction", function.MangledName);
            Assert.Contains(function.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("was ambiguous"));
        }

        [NeedsLinuxOrClangFact]
        public void ErrorOnMissing_False()
        {
            LinkImportsTransformation transformation = new()
            {
                ErrorOnMissing = false
            };

            CreateSharedLibrary(transformation, nameof(ErrorOnMissing_False), "UnrelatedFunction");
            TranslatedLibrary library = CreateLibrary(@"extern ""C"" void TestFunction();");
            library = transformation.Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.NotEqual($"{nameof(ErrorOnMissing_False)}.so", function.DllFileName);
            Assert.Empty(function.Diagnostics);
        }

        [NeedsLinuxOrClangFact]
        public void ErrorOnMissing_True()
        {
            LinkImportsTransformation transformation = new()
            {
                ErrorOnMissing = true
            };

            CreateSharedLibrary(transformation, nameof(ErrorOnMissing_True), "UnrelatedFunction");
            TranslatedLibrary library = CreateLibrary(@"extern ""C"" void TestFunction();");
            library = transformation.Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.NotEqual($"{nameof(ErrorOnMissing_True)}.so", function.DllFileName);
            Assert.Contains(function.Diagnostics, d => d.Severity == Severity.Error && d.Message.Contains("Could not resolve"));
        }

        [NeedsLinuxOrClangFact]
        public void CodeResolvesToDataSymbol()
        {
            LinkImportsTransformation transformation = new();
            CreateSharedLibrary(transformation, nameof(CodeResolvesToDataSymbol), "TestFunction.data");
            TranslatedLibrary library = CreateLibrary(@"extern ""C"" void TestFunction();");
            library = transformation.Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.Equal($"{nameof(CodeResolvesToDataSymbol)}.so", function.DllFileName);
            Assert.Equal("TestFunction", function.MangledName);
            Assert.Contains(function.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("resolved to non-code symbol"));
        }

        [NeedsLinuxOrClangFact]
        public void DataResolvesToCodeSymbol()
        {
            LinkImportsTransformation transformation = new();
            CreateSharedLibrary(transformation, nameof(DataResolvesToCodeSymbol), "TestGlobal");
            TranslatedLibrary library = CreateLibrary(@"extern ""C"" int TestGlobal;");
            library = transformation.Transform(library);

            TranslatedStaticField staticField = library.FindDeclaration<TranslatedStaticField>("TestGlobal");
            Assert.Equal($"{nameof(DataResolvesToCodeSymbol)}.so", staticField.DllFileName);
            Assert.Equal("TestGlobal", staticField.MangledName);
            Assert.Contains(staticField.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("resolved to a code symbol"));
        }

        [NeedsLinuxOrClangFact]
        public void TrackVerboseImportInformation_False()
        {
            LinkImportsTransformation transformation = new()
            {
                WarnOnAmbiguousSymbols = true,
                TrackVerboseImportInformation = false
            };

            CreateSharedLibrary(transformation, $"{nameof(TrackVerboseImportInformation_False)}_0", "TestFunction");
            CreateSharedLibrary(transformation, $"{nameof(TrackVerboseImportInformation_False)}_1", "TestFunction");
            TranslatedLibrary library = CreateLibrary(@"extern ""C"" void TestFunction();");
            library = transformation.Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.Equal($"{nameof(TrackVerboseImportInformation_False)}_0.so", function.DllFileName);
            Assert.Equal("TestFunction", function.MangledName);

            Assert.Single(function.Diagnostics);
            TranslationDiagnostic diagnostic = function.Diagnostics[0];
            Assert.Equal(Severity.Warning, diagnostic.Severity);
            Assert.Contains("was ambiguous", diagnostic.Message);
            Assert.DoesNotContain($"./{nameof(TrackVerboseImportInformation_False)}_0.so", diagnostic.Message);
            Assert.DoesNotContain($"./{nameof(TrackVerboseImportInformation_False)}_1.so", diagnostic.Message);
        }

        [NeedsLinuxOrClangFact]
        public void TrackVerboseImportInformation_True()
        {
            LinkImportsTransformation transformation = new()
            {
                WarnOnAmbiguousSymbols = true,
                TrackVerboseImportInformation = true
            };

            CreateSharedLibrary(transformation, $"{nameof(TrackVerboseImportInformation_True)}_0", "TestFunction");
            CreateSharedLibrary(transformation, $"{nameof(TrackVerboseImportInformation_True)}_1", "TestFunction");
            TranslatedLibrary library = CreateLibrary(@"extern ""C"" void TestFunction();");
            library = transformation.Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.Equal($"{nameof(TrackVerboseImportInformation_True)}_0.so", function.DllFileName);
            Assert.Equal("TestFunction", function.MangledName);

            Assert.Single(function.Diagnostics);
            TranslationDiagnostic diagnostic = function.Diagnostics[0];
            Assert.Equal(Severity.Warning, diagnostic.Severity);
            Assert.Contains("was ambiguous", diagnostic.Message);
            Assert.Contains($"./{nameof(TrackVerboseImportInformation_True)}_0.so", diagnostic.Message);
            Assert.Contains($"./{nameof(TrackVerboseImportInformation_True)}_1.so", diagnostic.Message);

            // Ensure that the error message does not contain the redundant DllFileName
            // (We do this with Windows libraries since they can be different, but this isn't a thing with ELF shared libraries.)
            // (We only check the _1 library because the _0 library appears in the initial message irregardless of TrackVerboseImportInformation.)
            Assert.DoesNotContain($"'{nameof(TrackVerboseImportInformation_True)}_1.so'", diagnostic.Message);
        }

        [NeedsLinuxOrClangFact]
        public void TrackVerboseImportInformation_MustBeSetBeforeAddingAnyLibraries()
        {
            LinkImportsTransformation transformation = new();
            CreateSharedLibrary(transformation, nameof(TrackVerboseImportInformation_MustBeSetBeforeAddingAnyLibraries), "TestFunction");
            Assert.Throws<InvalidOperationException>(() => transformation.TrackVerboseImportInformation = true);
        }

        [NeedsLinuxOrClangFact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/136")]
        public void VirtualMethod_NoErrorOnMissing()
        {
            LinkImportsTransformation transformation = new()
            {
                ErrorOnMissing = true
            };

            TranslatedLibrary library = CreateLibrary(@"
class Test
{
public:
    virtual void VirtualMethod();
};
"
            );

            TranslatedFunction methodBeforeTransformation = library.FindDeclaration<TranslatedRecord>("Test").FindDeclaration<TranslatedFunction>("VirtualMethod");
            library = transformation.Transform(library);
            TranslatedFunction methodAfterTransformation = library.FindDeclaration<TranslatedRecord>("Test").FindDeclaration<TranslatedFunction>("VirtualMethod");

            Assert.ReferenceEqual(methodBeforeTransformation, methodAfterTransformation);
        }

        [NeedsLinuxOrClangFact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/136")]
        public void VirtualMethod_ErrorOnMissing()
        {
            LinkImportsTransformation transformation = new()
            {
                ErrorOnMissing = true,
                ErrorOnMissingVirtualMethods = true
            };

            TranslatedLibrary library = CreateLibrary(@"
class Test
{
public:
    virtual void VirtualMethod();
};
"
            );

            library = transformation.Transform(library);
            TranslatedFunction virtualMethod = library.FindDeclaration<TranslatedRecord>("Test").FindDeclaration<TranslatedFunction>("VirtualMethod");
            Assert.Contains(virtualMethod.Diagnostics, d => d.IsError && d.Message.Contains("Could not resolve"));
        }

        [NeedsLinuxOrClangFact]
        public void GlobalSymbolDoesNotSupersedeWeakSymbol()
        {
            // The description of global vs weak symbols implies that global symbols should replace weak symbols.
            // However, this feature is only intended for static linking, it should not apply during dynamic linking.
            // https://www.bottomupcs.com/libraries_and_the_linker.xhtml#d0e10440
            // In theory we could implement an option to provide LD_DYNAMIC_WEAK-like behavior, but the complaint behavior has been standard since 1999. I doubt anyone expects it anymore.
            LinkImportsTransformation transformation = new();
            CreateSharedLibrary(transformation, $"{nameof(GlobalSymbolDoesNotSupersedeWeakSymbol)}_0", "~TestFunction");
            CreateSharedLibrary(transformation, $"{nameof(GlobalSymbolDoesNotSupersedeWeakSymbol)}_1", "TestFunction");
            TranslatedLibrary library = CreateLibrary(@"extern ""C"" void TestFunction();");
            library = transformation.Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.Equal($"{nameof(GlobalSymbolDoesNotSupersedeWeakSymbol)}_0.so", function.DllFileName);
            Assert.Equal("TestFunction", function.MangledName);
        }
    }
}
