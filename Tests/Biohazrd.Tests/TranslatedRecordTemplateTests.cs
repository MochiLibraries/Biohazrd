using Biohazrd.Expressions;
using Biohazrd.Tests.Common;
using ClangSharp;
using ClangSharp.Interop;
using System.Linq;
using Xunit;

namespace Biohazrd.Tests;

public sealed class TranslatedRecordTemplateTests : BiohazrdTestBase
{
    [Fact]
    public void BasicClassParameter()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class T>
struct MyTemplate
{
    T MyField;
};
"
        );
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
        Assert.Equal(RecordKind.Struct, template.Kind);
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
struct MyTemplate
{
    T MyField;
};
"
        );
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
        Assert.Equal(RecordKind.Struct, template.Kind);
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
struct MyTemplate
{
    int Field[x];
};
"
        );
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
        Assert.Equal(RecordKind.Struct, template.Kind);
        TranslatedTemplateParameter parameter = Assert.Single(template.Parameters);
        Assert.Equal("x", parameter.Name);
        Assert.False(parameter.IsParameterPack);

        TranslatedTemplateConstantParameter constantParameter = Assert.IsType<TranslatedTemplateConstantParameter>(parameter);
        ClangTypeReference clangType = Assert.IsAssignableFrom<ClangTypeReference>(constantParameter.Type);
        Assert.Equal(CXTypeKind.CXType_Int, clangType.ClangType.Kind);
        Assert.Null(constantParameter.DefaultValue);
    }

    [Fact]
    public void BasicClassTemplate()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class T>
class MyTemplate
{
public:
    T MyField;
};
"
        );
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
        Assert.Equal(RecordKind.Class, template.Kind);
    }

    [Fact]
    public void BasicUnionTemplate()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class T>
union MyTemplate
{
    T MyField;
};
"
        );
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
        Assert.Equal(RecordKind.Union, template.Kind);
    }

    [Fact]
    public void UndefinedClassTemplate()
    {
        TranslatedLibrary library = CreateLibrary(@"template<class T> class MyTemplate;");
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
        Assert.Equal(RecordKind.Class, template.Kind);
        TranslatedTemplateParameter parameter = Assert.Single(template.Parameters);
        Assert.Equal("T", parameter.Name);
        Assert.IsType<TranslatedTemplateTypeParameter>(parameter);
    }

    [Fact]
    public void UndefinedStructTemplate()
    {
        TranslatedLibrary library = CreateLibrary(@"template<class T> struct MyTemplate;");
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
        Assert.Equal(RecordKind.Struct, template.Kind);
        TranslatedTemplateParameter parameter = Assert.Single(template.Parameters);
        Assert.Equal("T", parameter.Name);
        Assert.IsType<TranslatedTemplateTypeParameter>(parameter);
    }

    [Fact]
    public void UndefinedUnionTemplate()
    {
        TranslatedLibrary library = CreateLibrary(@"template<class T> union MyTemplate;");
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
        Assert.Equal(RecordKind.Union, template.Kind);
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
    struct MyTemplate
    {
        T MyField;
    };
}
"
        );
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>();
        Assert.Equal("MyNamespace", template.Namespace);
        Assert.Equal("MyTemplate", template.Name);
    }

    [Fact]
    public void MultipleTemplateParameters()
    {
        TranslatedLibrary library = CreateLibrary
        (@"
template<class T, class U, int x, short y>
struct MyTemplate
{
    T MyField;
    U MyField2;
    int MyField3 = x;
    short MyField4 = y;
};
"
        );

        //TODO: Assert null default values
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
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
struct MyTemplate
{
    T MyField = x;
};
"
        );
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
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
struct MyTemplate;
"
        );
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
        Assert.Equal(RecordKind.Struct, template.Kind);
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
struct MyTemplate;
"
        );
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
        Assert.Equal(RecordKind.Struct, template.Kind);
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
template<int... x>
struct MyTemplate;
"
        );
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
        Assert.Equal(RecordKind.Struct, template.Kind);
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
        TranslatedLibrary library = CreateLibrary(@"template<class T = int> struct MyTemplate;");
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
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
        TranslatedLibrary library = CreateLibrary(@"template<int x = 3226> struct MyTemplate;");
        TranslatedRecordTemplate template = library.FindDeclaration<TranslatedRecordTemplate>("MyTemplate");
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
struct MyTemplate;

template<class T>
struct MyTemplate
{
    T MyField;
};
"
        );
        Assert.Single(library.Declarations.OfType<TranslatedRecordTemplate>());
    }
}
