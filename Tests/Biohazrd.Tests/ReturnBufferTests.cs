using Biohazrd.Tests.Common;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class ReturnBufferTests : BiohazrdTestBase
    {
        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/142")]
        public void EnregisterableRecord_Windows()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct MyInt
{
    int X;
};

MyInt GlobalFunction();

class MyClass
{
public:
    static MyInt StaticMethod();
    MyInt InstanceMethod();
    virtual MyInt VirtualMethod();
};
"
            );

            TranslatedFunction globalFunction = library.FindDeclaration<TranslatedFunction>("GlobalFunction");
            TranslatedRecord myClass = library.FindDeclaration<TranslatedRecord>("MyClass");
            TranslatedFunction staticMethod = myClass.FindDeclaration<TranslatedFunction>("StaticMethod");
            TranslatedFunction instanceMethod = myClass.FindDeclaration<TranslatedFunction>("InstanceMethod");
            TranslatedFunction virtualMethod = myClass.FindDeclaration<TranslatedFunction>("VirtualMethod");
            TranslatedVTable vTable = Assert.NotNull(myClass.VTable);
            TranslatedVTableEntry vTableFunction = Assert.Single(vTable.Entries, e => e.IsFunctionPointer);
            FunctionPointerTypeReference vTableFunctionPointerType = Assert.IsType<FunctionPointerTypeReference>(vTableFunction.Type);

            // https://docs.microsoft.com/en-us/cpp/build/x64-calling-convention?view=msvc-160#return-values
            // "User-defined types can be returned by value from global functions and static member functions."
            // Note the lack of instance methods in this list.
            // See https://github.com/InfectedLibraries/Biohazrd/issues/142 for details.
            Assert.False(globalFunction.ReturnByReference);
            Assert.False(staticMethod.ReturnByReference);
            Assert.True(instanceMethod.ReturnByReference);
            Assert.True(virtualMethod.ReturnByReference);

            // VTable entries don't directly expose that they're return by reference.
            // However, you can observe that they are as a side effect of the return type becoming a pointer and being added as a second parameter.
            Assert.IsType<PointerTypeReference>(vTableFunctionPointerType.ReturnType);
            Assert.Equal(2, vTableFunctionPointerType.ParameterTypes.Length); // (this, retbuf)
            Assert.Equal(vTableFunctionPointerType.ReturnType, vTableFunctionPointerType.ParameterTypes[1]);
        }
    }
}
