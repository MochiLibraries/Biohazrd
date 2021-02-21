using Biohazrd.Tests.Common;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class TranslatedFunctionTests : BiohazrdTestBase
    {
        [Fact]
        public void SpecialFunctionKind_NormalFunction()
        {
            TranslatedLibrary library = CreateLibrary("void Function();");
            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.None, function.SpecialFunctionKind);
        }

        [Fact]
        public void SpecialFunctionKind_NormalMethod()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class MyClass
{
public:
    void Method();
};
"
            );
            TranslatedFunction method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.None, method.SpecialFunctionKind);
        }

        [Fact]
        public void SpecialFunctionKind_Constructor()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class MyClass
{
public:
    MyClass();
};
"
            );
            TranslatedFunction method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.Constructor, method.SpecialFunctionKind);
        }

        [Fact]
        public void SpecialFunctionKind_Destructor()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class MyClass
{
public:
    ~MyClass();
};
"
            );
            TranslatedFunction method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.Destructor, method.SpecialFunctionKind);
        }

        [Fact]
        public void SpecialFunctionKind_OperatorOverloadFunction()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct MyStruct { };
bool operator==(MyStruct, MyStruct);
"
            );
            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.OperatorOverload, function.SpecialFunctionKind);
        }

        [Fact]
        public void SpecialFunctionKind_OperatorOverloadMethod()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class MyClass
{
public:
    int operator[](int i);
};
"
            );
            TranslatedFunction method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.OperatorOverload, method.SpecialFunctionKind);
        }

        [Fact]
        public void SpecialFunctionKind_ConversionOverload()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class MyClass
{
public:
    operator int();
};
"
            );
            TranslatedFunction method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.ConversionOverload, method.SpecialFunctionKind);
        }
    }
}
