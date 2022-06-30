using Biohazrd.Expressions;
using Biohazrd.Tests.Common;
using ClangSharp;
using ClangSharp.Interop;
using System.Linq;
using Xunit;

namespace Biohazrd.Tests;

public sealed class TranslatedFunctionTemplateTests : BiohazrdTestBase
{
    [Fact]
    public void BasicClassParameter()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class T>
T MyTemplate(T a, T b)
{
    return a + b;
}
"
        );
        TranslatedFunctionTemplate template = library.FindDeclaration<TranslatedFunctionTemplate>("MyTemplate");
        TranslatedTemplateParameter parameter = Assert.Single(template.Parameters);
        Assert.Equal("T", parameter.Name);
        TranslatedTemplateTypeParameter typeParameter = Assert.IsType<TranslatedTemplateTypeParameter>(parameter);
        Assert.False(parameter.IsParameterPack);
        Assert.Null(typeParameter.DefaultType);
    }

    [Fact]
    public void BasicTypeNameParameter()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<typename T>
T MyTemplate(T a, T b)
{
    return a + b;
}
"
        );
        TranslatedFunctionTemplate template = library.FindDeclaration<TranslatedFunctionTemplate>("MyTemplate");
        TranslatedTemplateParameter parameter = Assert.Single(template.Parameters);
        Assert.Equal("T", parameter.Name);
        TranslatedTemplateTypeParameter typeParameter = Assert.IsType<TranslatedTemplateTypeParameter>(parameter);
        Assert.False(parameter.IsParameterPack);
        Assert.Null(typeParameter.DefaultType);
    }

    [Fact]
    public void BasicConstantParameter()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<int x>
int MyTemplate()
{
    return x;
}
"
        );
        TranslatedFunctionTemplate template = library.FindDeclaration<TranslatedFunctionTemplate>("MyTemplate");
        TranslatedTemplateParameter parameter = Assert.Single(template.Parameters);
        Assert.Equal("x", parameter.Name);
        Assert.False(parameter.IsParameterPack);

        TranslatedTemplateConstantParameter constantParameter = Assert.IsType<TranslatedTemplateConstantParameter>(parameter);
        ClangTypeReference clangType = Assert.IsAssignableFrom<ClangTypeReference>(constantParameter.Type);
        Assert.Equal(CXTypeKind.CXType_Int, clangType.ClangType.Kind);
        Assert.Null(constantParameter.DefaultValue);
    }

    [Fact]
    public void UndefinedTemplate()
    {
        TranslatedLibrary library = CreateLibrary(@"template<class T> T MyTemplate(T a, T b);");
        TranslatedFunctionTemplate template = library.FindDeclaration<TranslatedFunctionTemplate>("MyTemplate");
        TranslatedTemplateParameter parameter = Assert.Single(template.Parameters);
        Assert.Equal("T", parameter.Name);
        Assert.IsType<TranslatedTemplateTypeParameter>(parameter);
    }

    [Fact]
    public void TemplateInNamespaceGetsCorrectName()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
namespace MyNamespace
{
    template<class T>
    T MyTemplate(T a, T b)
    {
        return a + b;
    }
}
"
        );
        TranslatedFunctionTemplate template = library.FindDeclaration<TranslatedFunctionTemplate>();
        Assert.Equal("MyNamespace", template.Namespace);
        Assert.Equal("MyTemplate", template.Name);
    }

    [Fact]
    public void MultipleTemplateParameters()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class T, class U, int x, short y>
void MyTemplate(T t, U u, int a = x, short b = y);
"
        );

        //TODO: Assert null default values
        TranslatedFunctionTemplate template = library.FindDeclaration<TranslatedFunctionTemplate>("MyTemplate");
        Assert.Equal(4, template.Parameters.Length);

        TranslatedTemplateTypeParameter parameter0 = Assert.IsType<TranslatedTemplateTypeParameter>(template.Parameters[0]);
        Assert.Equal("T", parameter0.Name);
        Assert.False(parameter0.IsParameterPack);
        Assert.Null(parameter0.DefaultType);

        TranslatedTemplateTypeParameter parameter1 = Assert.IsType<TranslatedTemplateTypeParameter>(template.Parameters[1]);
        Assert.Equal("U", parameter1.Name);
        Assert.False(parameter1.IsParameterPack);
        Assert.Null(parameter1.DefaultType);

        TranslatedTemplateConstantParameter parameter2 = Assert.IsType<TranslatedTemplateConstantParameter>(template.Parameters[2]);
        Assert.Equal("x", parameter2.Name);
        ClangTypeReference clangType2 = Assert.IsAssignableFrom<ClangTypeReference>(parameter2.Type);
        Assert.Equal(CXTypeKind.CXType_Int, clangType2.ClangType.Kind);
        Assert.False(parameter2.IsParameterPack);
        Assert.Null(parameter2.DefaultValue);

        TranslatedTemplateConstantParameter parameter3 = Assert.IsType<TranslatedTemplateConstantParameter>(template.Parameters[3]);
        Assert.Equal("y", parameter3.Name);
        ClangTypeReference clangType3 = Assert.IsAssignableFrom<ClangTypeReference>(parameter3.Type);
        Assert.Equal(CXTypeKind.CXType_Short, clangType3.ClangType.Kind);
        Assert.False(parameter3.IsParameterPack);
        Assert.Null(parameter3.DefaultValue);
    }

    [Fact]
    public void SelfReferencingTemplateParameter()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class T, T x>
T MyTemplate(T a = x)
{
    return a * 2;
}
"
        );
        TranslatedFunctionTemplate template = library.FindDeclaration<TranslatedFunctionTemplate>("MyTemplate");
        Assert.Equal(2, template.Parameters.Length);

        TranslatedTemplateTypeParameter parameter0 = Assert.IsType<TranslatedTemplateTypeParameter>(template.Parameters[0]);
        Assert.Equal("T", parameter0.Name);

        TranslatedTemplateConstantParameter parameter1 = Assert.IsType<TranslatedTemplateConstantParameter>(template.Parameters[1]);
        Assert.Equal("x", parameter1.Name);
        ClangTypeReference clangType1 = Assert.IsAssignableFrom<ClangTypeReference>(parameter1.Type);
        TemplateTypeParmType templateType = Assert.IsAssignableFrom<TemplateTypeParmType>(clangType1.ClangType);
        Assert.NotNull(parameter0.Declaration);
        Assert.ReferenceEqual(parameter0.Declaration, templateType.Decl);
    }

    [Fact]
    public void ParameterPackClassParameter()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class... T>
void MyTemplate(T... args);
"
        );
        TranslatedFunctionTemplate template = library.FindDeclaration<TranslatedFunctionTemplate>("MyTemplate");
        TranslatedTemplateParameter parameter = Assert.Single(template.Parameters);
        Assert.Equal("T", parameter.Name);
        Assert.IsType<TranslatedTemplateTypeParameter>(parameter);
        Assert.True(parameter.IsParameterPack);
    }

    [Fact]
    public void ParameterPackTypeNameParameter()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<typename... T>
void MyTemplate(T... args);
"
        );
        TranslatedFunctionTemplate template = library.FindDeclaration<TranslatedFunctionTemplate>("MyTemplate");
        TranslatedTemplateParameter parameter = Assert.Single(template.Parameters);
        Assert.Equal("T", parameter.Name);
        Assert.IsType<TranslatedTemplateTypeParameter>(parameter);
        Assert.True(parameter.IsParameterPack);
    }

    [Fact]
    public void ParameterPackConstantParameter()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class... T>
void OtherTemplate(T... args);

template<int... x>
void MyTemplate()
{
    OtherTemplate(x...);
}
"
        );
        TranslatedFunctionTemplate template = library.FindDeclaration<TranslatedFunctionTemplate>("MyTemplate");
        TranslatedTemplateParameter parameter = Assert.Single(template.Parameters);
        Assert.Equal("x", parameter.Name);
        Assert.IsType<TranslatedTemplateConstantParameter>(parameter);
        Assert.True(parameter.IsParameterPack);

        TranslatedTemplateConstantParameter constantParameter = Assert.IsType<TranslatedTemplateConstantParameter>(parameter);
        ClangTypeReference clangType = Assert.IsAssignableFrom<ClangTypeReference>(constantParameter.Type);
        Assert.Equal(CXTypeKind.CXType_Int, clangType.ClangType.Kind);
    }

    [Fact]
    public void DefaultTypeParameter()
    {
        TranslatedLibrary library = CreateLibrary(@"template<class T = int> T MyTemplate(T a, T b);");
        TranslatedFunctionTemplate template = library.FindDeclaration<TranslatedFunctionTemplate>("MyTemplate");
        TranslatedTemplateParameter parameter = Assert.Single(template.Parameters);
        Assert.Equal("T", parameter.Name);
        TranslatedTemplateTypeParameter typeParameter = Assert.IsType<TranslatedTemplateTypeParameter>(parameter);

        TypeReference defaultType = Assert.NotNull(typeParameter.DefaultType);
        ClangTypeReference defaultClangType = Assert.IsAssignableFrom<ClangTypeReference>(defaultType);
        Assert.Equal(CXTypeKind.CXType_Int, defaultClangType.ClangType.Kind);
    }

    [Fact]
    public void DefaultConstantParameter()
    {
        TranslatedLibrary library = CreateLibrary(@"template<int x = 3226> void MyTemplate(int a = x);");
        TranslatedFunctionTemplate template = library.FindDeclaration<TranslatedFunctionTemplate>("MyTemplate");
        TranslatedTemplateParameter parameter = Assert.Single(template.Parameters);
        Assert.Equal("x", parameter.Name);
        TranslatedTemplateConstantParameter constantParameter = Assert.IsType<TranslatedTemplateConstantParameter>(parameter);

        ConstantValue defaultValue = Assert.NotNull(constantParameter.DefaultValue);
        IntegerConstant integerValue = Assert.IsAssignableFrom<IntegerConstant>(defaultValue);
        Assert.Equal(3226ul, integerValue.Value);
        Assert.True(integerValue.IsSigned);
        Assert.Equal(sizeof(int) * 8, integerValue.SizeBits);
    }

    [Fact]
    public void ForwardDeclaredAndDefinedTemplateIsTranslatedOnce()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class T>
T MyTemplate(T a, T b);

template<class T>
T MyTemplate(T a, T b)
{
    return a + b;
}
"
        );
        Assert.Single(library.Declarations.OfType<TranslatedFunctionTemplate>());
    }

    [Fact]
    public void IsInstanceMethodTest()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class T>
T GlobalTemplate(T a, T b);

class MyClass
{
public:
    template<class T>
    T InstanceMethod(T a, T b);

    template<class T>
    static T StaticMethod(T a, T b);
};
"
        );

        TranslatedRecord myClass = library.FindDeclaration<TranslatedRecord>("MyClass");

        Assert.False(library.FindDeclaration<TranslatedFunctionTemplate>("GlobalTemplate").IsInstanceMethod);
        Assert.True(myClass.FindDeclaration<TranslatedFunctionTemplate>("InstanceMethod").IsInstanceMethod);
        Assert.False(myClass.FindDeclaration<TranslatedFunctionTemplate>("StaticMethod").IsInstanceMethod);
    }

    [Fact]
    public void IsConst()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
class MyClass
{
    template<class T>
    T NonConstTemplate(T a, T b);

    template<class T>
    T ConstTemplate(T a, T b) const;
};
"
        );
        TranslatedRecord myClass = library.FindDeclaration<TranslatedRecord>("MyClass");
        Assert.False(myClass.FindDeclaration<TranslatedFunctionTemplate>("NonConstTemplate").IsConst);
        Assert.True(myClass.FindDeclaration<TranslatedFunctionTemplate>("ConstTemplate").IsConst);
    }

    [Fact]
    public void IsInline()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class T>
T NotInline(T a, T b);

template<class T>
T NotInline2(T a, T b)
{
    return a + b;
}

template<class T>
inline T InlineFunction(T a, T b)
{
    return a + b;
}
"
        );
        Assert.False(library.FindDeclaration<TranslatedFunctionTemplate>("NotInline").IsInline);
        Assert.False(library.FindDeclaration<TranslatedFunctionTemplate>("NotInline2").IsInline);
        Assert.True(library.FindDeclaration<TranslatedFunctionTemplate>("InlineFunction").IsInline);
    }

    [Fact]
    public void IsInline_MsvcForceInline()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class T>
__forceinline T MyTemplate(T a, T b)
{
    return a + b;
}
",
            targetTriple: "x86_64-pc-win32"
        );
        Assert.True(library.FindDeclaration<TranslatedFunctionTemplate>("MyTemplate").IsInline);
    }

#if false // always_inline seems to not work with template functions. (Maybe it works with its specializations?)
    [Fact]
    public void IsInline_GccAlwaysInline()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class T>
__attribute__((always_inline)) T MyTemplate(T a, T b)
{
    return a + b;
}
",
            targetTriple: "x86_64-pc-linux"
        );
        Assert.True(library.FindDeclaration<TranslatedFunctionTemplate>("MyTemplate").IsInline);
    }
#endif

    [Fact]
    public void IsInline_ImplicitInline()
    {
        TranslatedLibrary library = CreateLibrary
       (@"
template<class T>
T Function(T a, T b)
{
    return a + b
}

class MyClass
{
public:
    template<class T>
    T Method(T a, T b)
    {
        return a + b
    }
};
"
        );
        Assert.False(library.FindDeclaration<TranslatedFunctionTemplate>("Function").IsInline);
        // Class methods with bodies are implicitly inline (n4659§12.2.1 Member Functions)
        // https://timsong-cpp.github.io/cppwp/n4659/class.mfct#1
        Assert.True(library.FindDeclaration<TranslatedRecord>("MyClass").FindDeclaration<TranslatedFunctionTemplate>("Method").IsInline);
    }

    [Fact]
    public void SpecialFunctionKind_NormalFunction()
    {
        TranslatedLibrary library = CreateLibrary("template<class T> void MyTemplate(T a, T b);");
        TranslatedFunctionTemplate function = library.FindDeclaration<TranslatedFunctionTemplate>();
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
    template<class T>
    void MyTemplate(T a, T b);
};
"
        );
        TranslatedFunctionTemplate method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunctionTemplate>();
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
    template<class T>
    MyClass();
};
"
        );
        TranslatedFunctionTemplate method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunctionTemplate>();
        Assert.Equal(SpecialFunctionKind.Constructor, method.SpecialFunctionKind);
    }

    [Fact]
    public void SpecialFunctionKind_OperatorOverloadFunction()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class T>
bool operator==(T, T);
"
        );
        TranslatedFunctionTemplate function = library.FindDeclaration<TranslatedFunctionTemplate>();
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
    template<class T>
    int operator[](T i);
};
"
        );
        TranslatedFunctionTemplate method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunctionTemplate>();
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
    template<class T>
    operator T();
};
"
        );
        TranslatedFunctionTemplate method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunctionTemplate>();
        Assert.Equal(SpecialFunctionKind.ConversionOverload, method.SpecialFunctionKind);
    }
}
