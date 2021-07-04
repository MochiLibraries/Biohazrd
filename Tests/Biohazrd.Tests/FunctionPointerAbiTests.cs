using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Common;
using ClangSharp.Pathogen;
using System.Collections.Immutable;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class FunctionPointerAbiTests : BiohazrdTestBase
    {
        private const string WindowsTarget = "x86_64-pc-win32";
        private const string LinuxTarget = "x86_64-pc-linux";

        private FunctionPointerTypeReference GetFunctionPointer(string cppCode, string targetTriple, out ImmutableArray<TranslationDiagnostic> diagnostics, TranslationOptions? options = null)
        {
            TranslatedLibrary library = CreateLibrary(cppCode, targetTriple: targetTriple, options);
            library = new TypeReductionTransformation().Transform(library);
            TranslatedStaticField? testField = null;

            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (declaration is not TranslatedStaticField { Name: "Test" } fieldDeclaration)
                { continue; }

                Assert.Null(testField);
                testField = fieldDeclaration;
            }

            Assert.NotNull(testField);
            diagnostics = testField.Diagnostics;
            return Assert.IsAssignableFrom<FunctionPointerTypeReference>(testField.Type);
        }

        private FunctionPointerTypeReference GetFunctionPointer(string cppCode, string targetTriple, TranslationOptions? options = null)
            => GetFunctionPointer(cppCode, targetTriple, diagnostics: out _, options);

        private FunctionPointerTypeReference GetFunctionPointerWindows(string cppCode)
            => GetFunctionPointer(cppCode, WindowsTarget);

        private FunctionPointerTypeReference GetFunctionPointerLinux(string cppCode)
            => GetFunctionPointer(cppCode, LinuxTarget);

        private (FunctionPointerTypeReference Windows, FunctionPointerTypeReference Linux) GetFunctionPointers(string cppCode)
            => (GetFunctionPointerWindows(cppCode), GetFunctionPointerLinux(cppCode));

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void BasicTest(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer("void (*Test)();", target);
            Assert.NotNull(function.FunctionAbi);
            Assert.True(function.IsCallable);
            Assert.Equal(0u, function.FunctionAbi.ArgumentCount);
            Assert.Equal(PathogenArgumentKind.Ignore, function.FunctionAbi.ReturnInfo.Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void ReturnIntrinisic(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer("int (*Test)();", target);
            Assert.NotNull(function.FunctionAbi);

            // Intrinsic types are returned directly via register
            Assert.Equal(PathogenArgumentKind.Direct, function.FunctionAbi.ReturnInfo.Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void ReturnPod(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer
            (@"
struct TestStruct { int a, b; };
TestStruct (*Test)();
",
                target
            );
            Assert.NotNull(function.FunctionAbi);

            // Word-sized POD types are returned directly via register from global functions
            Assert.Equal(PathogenArgumentKind.Direct, function.FunctionAbi.ReturnInfo.Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/186")]
        public void ReturnPodTooBig(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer
            (@"
struct TestStruct { int a, b, c, d, e, f, g; };
TestStruct (*Test)();
",
                target
            );
            Assert.NotNull(function.FunctionAbi);

            // POD types larger than the word size are always returned indirectly via a buffer
            Assert.Equal(PathogenArgumentKind.Indirect, function.FunctionAbi.ReturnInfo.Kind);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/186")]
        public void ReturnPodWithConstructor()
        {
            (FunctionPointerTypeReference windows, FunctionPointerTypeReference linux) = GetFunctionPointers
            (@"
struct TestStruct
{
    TestStruct() { }
    int a, b;
};
TestStruct (*Test)();
"
            );
            Assert.NotNull(windows.FunctionAbi);
            Assert.NotNull(linux.FunctionAbi);

            // Word-sized POD types with a constructor are returned directly via register from global functions on Itanium, but not Microsoft
            Assert.Equal(PathogenArgumentKind.Indirect, windows.FunctionAbi.ReturnInfo.Kind);
            Assert.Equal(PathogenArgumentKind.Direct, linux.FunctionAbi.ReturnInfo.Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void ParameterIntrinsic(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer("void (*Test)(int);", target);
            Assert.NotNull(function.FunctionAbi);

            // Intrinsic types are passed directly via register
            Assert.Equal(1u, function.FunctionAbi.ArgumentCount);
            Assert.Equal(PathogenArgumentKind.Direct, function.FunctionAbi.Arguments[0].Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void ParameterPod(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer
            (@"
struct TestStruct { int a, b; };
void (*Test)(TestStruct);
",
                target
            );
            Assert.NotNull(function.FunctionAbi);

            // Word-sized POD types are passed directly via register from global functions
            Assert.Equal(1u, function.FunctionAbi.ArgumentCount);
            Assert.Equal(PathogenArgumentKind.Direct, function.FunctionAbi.Arguments[0].Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void ParameterPodTooBig(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer
            (@"
struct TestStruct { int a, b, c, d, e, f, g; };
void (*Test)(TestStruct);
",
                target
            );
            Assert.NotNull(function.FunctionAbi);

            // POD types larger than the word size are always passed indirectly via a buffer
            Assert.Equal(1u, function.FunctionAbi.ArgumentCount);
            Assert.Equal(PathogenArgumentKind.Indirect, function.FunctionAbi.Arguments[0].Kind);
        }

        [Fact]
        public void ParameterPodWithConstructor()
        {
            (FunctionPointerTypeReference windows, FunctionPointerTypeReference linux) = GetFunctionPointers
            (@"
struct TestStruct
{
    TestStruct() { }
    int a, b;
};
void (*Test)(TestStruct);
"
            );
            Assert.NotNull(windows.FunctionAbi);
            Assert.NotNull(linux.FunctionAbi);

            // Unlike with returns, word-sized POD types with a constructor are passed directly via register to global functions in both ABIs
            Assert.Equal(1u, windows.FunctionAbi.ArgumentCount);
            Assert.Equal(1u, linux.FunctionAbi.ArgumentCount);
            Assert.Equal(PathogenArgumentKind.Direct, windows.FunctionAbi.Arguments[0].Kind);
            Assert.Equal(PathogenArgumentKind.Direct, linux.FunctionAbi.Arguments[0].Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/134")]
        public void FunctionIsUncallableWithIncompleteReturn(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer
            (@"
struct UndefinedType;
UndefinedType (*Test)();
",
                target,
                diagnostics: out ImmutableArray<TranslationDiagnostic> diagnostics
            );

            Assert.Null(function.FunctionAbi);
            Assert.False(function.IsCallable);
            Assert.Contains(diagnostics, d => d.Severity >= Severity.Warning && d.Message.StartsWith("Function pointer is not callable"));
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/134")]
        public void FunctionIsUncallableWithIncompleteParameter(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer
            (@"
struct UndefinedType;
void (*Test)(UndefinedType x);
",
                target,
                diagnostics: out ImmutableArray<TranslationDiagnostic> diagnostics
            );

            Assert.Null(function.FunctionAbi);
            Assert.False(function.IsCallable);
            Assert.Contains(diagnostics, d => d.Severity >= Severity.Warning && d.Message.StartsWith("Function pointer is not callable"));
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/134")]
        public void FunctionIsCallableWithPointerToIncompleteReturn(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer
            (@"
struct UndefinedType;
UndefinedType* (*Test)();
",
                target,
                diagnostics: out ImmutableArray<TranslationDiagnostic> diagnostics
            );

            Assert.NotNull(function.FunctionAbi);
            Assert.True(function.IsCallable);
            Assert.DoesNotContain(diagnostics, d => d.IsError);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/134")]
        public void FunctionIsCallableWithPointerToIncompleteParameter(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer
            (@"
struct UndefinedType;
void (*Test)(UndefinedType* x);
",
                target,
                diagnostics: out ImmutableArray<TranslationDiagnostic> diagnostics
            );

            Assert.NotNull(function.FunctionAbi);
            Assert.True(function.IsCallable);
            Assert.DoesNotContain(diagnostics, d => d.IsError);
            Assert.Equal(1u, function.FunctionAbi.ArgumentCount);
            Assert.Equal(PathogenArgumentKind.Direct, function.FunctionAbi.Arguments[0].Kind);
        }

#if false // These tests are broken by https://github.com/InfectedLibraries/Biohazrd/issues/195
        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/134")]
        public void FunctionIsUncallableWithIncompleteTemplateReturn(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer
            (@"
struct UndefinedType;

template<typename T> struct SomeTemplate
{
    T Field;
};

SomeTemplate<UndefinedType> (*Test)();
",
                target,
                diagnostics: out ImmutableArray<TranslationDiagnostic> diagnostics
            );

            Assert.Null(function.FunctionAbi);
            Assert.False(function.IsCallable);
            Assert.Contains(diagnostics, d => d.IsError && d.Message.StartsWith("Function is not callable"));
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/134")]
        public void FunctionIsCallableWithPointerToIncompleteTemplateReturn(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer
            (@"
struct UndefinedType;

template<typename T> struct SomeTemplate
{
    T Field;
};

SomeTemplate<UndefinedType>* (*Test)();
",
                target,
                diagnostics: out ImmutableArray<TranslationDiagnostic> diagnostics
            );

            Assert.NotNull(function.FunctionAbi);
            Assert.True(function.IsCallable);
            Assert.DoesNotContain(diagnostics, d => d.IsError);
        }
#endif

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/134")]
        public void FunctionIsCallableWithCompleteTemplateReturn(string target)
        {
            FunctionPointerTypeReference function = GetFunctionPointer
            (@"
template<typename T> struct SomeTemplate
{
    T Field;
};

SomeTemplate<int> (*Test)();
",
                target,
                diagnostics: out ImmutableArray<TranslationDiagnostic> diagnostics
            );

            Assert.NotNull(function.FunctionAbi);
            Assert.True(function.IsCallable);
            Assert.DoesNotContain(diagnostics, d => d.IsError);
        }
    }
}
