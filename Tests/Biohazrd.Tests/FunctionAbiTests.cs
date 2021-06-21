using Biohazrd.Tests.Common;
using ClangSharp.Interop;
using ClangSharp.Pathogen;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class FunctionAbiTests : BiohazrdTestBase
    {
        private const string WindowsTarget = "x86_64-pc-win32";
        private const string LinuxTarget = "x86_64-pc-linux";

        private TranslatedFunction GetFunction(string cppCode, string targetTriple)
        {
            TranslatedLibrary library = CreateLibrary(cppCode, targetTriple: targetTriple);
            TranslatedFunction? result = null;

            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (declaration is not TranslatedFunction { Name: "Test" } function)
                { continue; }

                Assert.Null(result);
                result = function;
            }

            Assert.NotNull(result);
            return result;
        }

        private TranslatedFunction GetFunctionWindows(string cppCode)
            => GetFunction(cppCode, WindowsTarget);

        private TranslatedFunction GetFunctionLinux(string cppCode)
            => GetFunction(cppCode, LinuxTarget);

        private (TranslatedFunction Windows, TranslatedFunction Linux) GetFunctions(string cppCode)
            => (GetFunctionWindows(cppCode), GetFunctionLinux(cppCode));

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void BasicTest(string target)
        {
            TranslatedFunction function = GetFunction("void Test();", target);
            Assert.Equal(0u, function.FunctionAbi.ArgumentCount);
            Assert.Equal(PathogenArgumentKind.Ignore, function.FunctionAbi.ReturnInfo.Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void ReturnIntrinisic(string target)
        {
            TranslatedFunction function = GetFunction("int Test();", target);

            // Intrinsic types are returned directly via register
            Assert.Equal(PathogenArgumentKind.Direct, function.FunctionAbi.ReturnInfo.Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void ReturnPod(string target)
        {
            TranslatedFunction function = GetFunction
            (@"
struct TestStruct { int a, b; };
TestStruct Test();
",
                target
            );

            // Word-sized POD types are returned directly via register from global functions
            Assert.Equal(PathogenArgumentKind.Direct, function.FunctionAbi.ReturnInfo.Kind);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/142")]
        public void ReturnPod_InstanceMethod()
        {
            (TranslatedFunction windows, TranslatedFunction linux) = GetFunctions
            (@"
struct TestStruct { int a, b; };

class TestClass
{
    TestStruct Test();
};
"
            );

            // Word-sized POD types are returned directly via register from instance methods on Itanium, but not Microsoft
            Assert.Equal(PathogenArgumentKind.Direct, linux.FunctionAbi.ReturnInfo.Kind);
            Assert.Equal(PathogenArgumentKind.Indirect, windows.FunctionAbi.ReturnInfo.Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void ReturnPodTooBig(string target)
        {
            TranslatedFunction function = GetFunction
            (@"
struct TestStruct { int a, b, c, d, e, f, g; };
TestStruct Test();
",
                target
            );

            // POD types larger than the word size are always returned indirectly via a buffer
            Assert.Equal(PathogenArgumentKind.Indirect, function.FunctionAbi.ReturnInfo.Kind);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/187")]
        public void ReturnPodWithConstructor()
        {
            (TranslatedFunction windows, TranslatedFunction linux) = GetFunctions
            (@"
struct TestStruct
{
    TestStruct() { }
    int a, b;
};
TestStruct Test();
"
            );

            // Word-sized POD types with a constructor are returned directly via register from global functions on Itanium, but not Microsoft
            Assert.Equal(PathogenArgumentKind.Indirect, windows.FunctionAbi.ReturnInfo.Kind);
            Assert.Equal(PathogenArgumentKind.Direct, linux.FunctionAbi.ReturnInfo.Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void ThisPointerTest(string target)
        {
            TranslatedFunction function = GetFunction
            (@"
struct TestStruct
{
    void Test();
};
",
                target
            );

            Assert.Empty(function.Parameters);
            Assert.Equal(1u, function.FunctionAbi.ArgumentCount);

            PathogenArgumentInfo thisArgument = function.FunctionAbi.Arguments[0];
            CXType thisPointerType = thisArgument.Type;
            Assert.Equal(CXTypeKind.CXType_Pointer, thisPointerType.kind);
            CXType thisInnerType = thisPointerType.PointeeType;
            Assert.Equal(CXTypeKind.CXType_Record, thisInnerType.kind);
            Assert.Equal("TestStruct", thisInnerType.Spelling.ToString());
        }

        [Fact]
        public void ReturnBufferThisPointerOrdering()
        {
            (TranslatedFunction windows, TranslatedFunction linux) = GetFunctions
            (
                @"
struct TestStruct { int a, b, c, d, e, f, g; };

class TestClass
{
    TestStruct Test();
};
"
            );

            // Sanity check the this pointer is present in the parameter list, but the return buffer is not
            Assert.Empty(windows.Parameters);
            Assert.Empty(linux.Parameters);
            Assert.Equal(1u, windows.FunctionAbi.ArgumentCount);
            Assert.Equal(1u, linux.FunctionAbi.ArgumentCount);

            // Sanity check returns are indirect
            Assert.Equal(PathogenArgumentKind.Indirect, windows.FunctionAbi.ReturnInfo.Kind);
            Assert.Equal(PathogenArgumentKind.Indirect, linux.FunctionAbi.ReturnInfo.Kind);

            // Check the retbuf & thisptr ordering, which is different between Microsoft and Itanium
            Assert.True(windows.FunctionAbi.ReturnInfo.Flags.HasFlag(PathogenArgumentFlags.IsSRetAfterThis));
            Assert.False(linux.FunctionAbi.ReturnInfo.Flags.HasFlag(PathogenArgumentFlags.IsSRetAfterThis));
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void ParameterIntrinsic(string target)
        {
            TranslatedFunction function = GetFunction("void Test(int);", target);

            // Intrinsic types are passed directly via register
            Assert.Equal(1u, function.FunctionAbi.ArgumentCount);
            Assert.Equal(PathogenArgumentKind.Direct, function.FunctionAbi.Arguments[0].Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void ParameterPod(string target)
        {
            TranslatedFunction function = GetFunction
            (@"
struct TestStruct { int a, b; };
void Test(TestStruct);
",
                target
            );

            // Word-sized POD types are passed directly via register from global functions
            Assert.Equal(1u, function.FunctionAbi.ArgumentCount);
            Assert.Equal(PathogenArgumentKind.Direct, function.FunctionAbi.Arguments[0].Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void ParameterPod_InstanceMethod(string target)
        {
            TranslatedFunction function = GetFunction
            (@"
struct TestStruct { int a, b; };

class TestClass
{
    void Test(TestStruct);
};
",
                target
            );

            // Unlike with return types, user-defined types are not passed differently for instance methods betewen Microsoft and Itanium
            Assert.Equal(2u, function.FunctionAbi.ArgumentCount);
            Assert.Equal(PathogenArgumentKind.Direct, function.FunctionAbi.Arguments[1].Kind);
        }

        [Theory]
        [InlineData(WindowsTarget)]
        [InlineData(LinuxTarget)]
        public void ParameterPodTooBig(string target)
        {
            TranslatedFunction function = GetFunction
            (@"
struct TestStruct { int a, b, c, d, e, f, g; };
void Test(TestStruct);
",
                target
            );

            // POD types larger than the word size are always passed indirectly via a buffer
            Assert.Equal(1u, function.FunctionAbi.ArgumentCount);
            Assert.Equal(PathogenArgumentKind.Indirect, function.FunctionAbi.Arguments[0].Kind);
        }

        [Fact]
        public void ParameterPodWithConstructor()
        {
            (TranslatedFunction windows, TranslatedFunction linux) = GetFunctions
            (@"
struct TestStruct
{
    TestStruct() { }
    int a, b;
};
void Test(TestStruct);
"
            );

            // Unlike with returns, word-sized POD types with a constructor are passed directly via register to global functions in both ABIs
            Assert.Equal(1u, linux.FunctionAbi.ArgumentCount);
            Assert.Equal(1u, windows.FunctionAbi.ArgumentCount);
            Assert.Equal(PathogenArgumentKind.Direct, windows.FunctionAbi.Arguments[0].Kind);
            Assert.Equal(PathogenArgumentKind.Direct, linux.FunctionAbi.Arguments[0].Kind);
        }
    }
}
