using Biohazrd.CSharp;
using Biohazrd.Tests.Common;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class TemplateTests : BiohazrdTestBase
    {
        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/153")]
        public void TemplateSpecializationWithoutConcreteUseCanBeTranslated()
        {
            // By default, Vec2l won't be instantiated within Clang, which causes a lot of information about it (such as its size or layout) to be unavailable.
            // This is because the implicit instantiation happens very lazily. If TestFunction were called or had a body, it'd be implicitly instantiated then.
            // This test ensure Biohazrd correctly forces it to become instantiated as expected.
            TranslatedLibrary library = CreateLibrary
            (@"
template<typename T> struct Vec4
{
    T x;
    T y;
    T z;
    T w;
};

typedef Vec4<short> Vec4s;
typedef Vec4<int> Vec4i;
typedef Vec4<long long> Vec4l;

void TestFunction(Vec4s a, Vec4i b, Vec4l c);

const int TestConstant = sizeof(Vec4s) + sizeof(Vec4i);
",
                targetTriple: "x86_64-pc-win32"
            );

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            TranslatedParameter a = function.FindDeclaration<TranslatedParameter>("a");
            TranslatedParameter b = function.FindDeclaration<TranslatedParameter>("b");
            TranslatedParameter c = function.FindDeclaration<TranslatedParameter>("c");
            Assert.False(a.ImplicitlyPassedByReference); // 64 bit enregistered struct is passed via register
            Assert.True(b.ImplicitlyPassedByReference); // 128 bit struct is passed by reference
            Assert.True(c.ImplicitlyPassedByReference); // 256 bit struct is passed by reference (Biohazrd will fail to identify this if the Clang quirk is not accounted for.)

            // There should be a note about Vec4l being late-instantiated
            // (The others should've been instantiated by Clang to calculate the value for TestConstant)
            Assert.Contains(library.ParsingDiagnostics, d => d.Severity == Severity.Note && d.Message == "Successfully late-instantiated 1 template specialization.");
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/153")]
        public void TemplateInstantiationErrorsAreEmitted()
        {
            TranslatedLibrary library = CreateLibraryBuilder
            (@"
template<typename T> struct TestTemplate
{
    T Field;
};
struct ForwardDeclaredType;
void TestFunction(TestTemplate<ForwardDeclaredType> x);
"
            ).Create();

            // There should be a warning about the failed instantiation and an error from Clang
            Assert.Contains(library.ParsingDiagnostics, d => d.Severity == Severity.Warning && d.Message == "Failed to late-instantiate 1 template specialization.");
            Assert.Contains(library.ParsingDiagnostics, d => d.Severity == Severity.Error && d.IsFromClang);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/153")]
        public void UndefinedTemplateIsIgnored()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
template<typename T> struct TestTemplate;
void TestFunction(TestTemplate<int> x);
"
            );

            TranslatedParameter parameter = library.FindDeclaration<TranslatedFunction>("TestFunction").FindDeclaration<TranslatedParameter>("x");
            ClangTypeReference clangType = Assert.IsType<ClangTypeReference>(parameter.Type);
            // Checking the size the type will fail since it's incomplete
            // (Also no errors will have been emitted earlier since we didn't try to instantiate an undefined template.)
            Assert.True(clangType.ClangType.Handle.SizeOf <= 0);
        }

        [Fact]
        public void ExplicitTemplateSpecialization()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
template<typename T> class MyTemplate
{
public:
    T Field;

    T Method(T parameter)
    {
        return parameter;
    }

    struct MyStruct
    {
        T NestedField;
    };
};

template<> class MyTemplate<int>
{
public:
    int SpecializedField1;
    int SpecializedField2;

    int SpecializedMethod(int specializedParameter)
    {
        return specializedParameter;
    }

    struct SpecializedStruct
    {
        int SpecializedNestedField;
    };
};
"
            );

            // Reduce types to make them easier to reason about
            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new CSharpBuiltinTypeTransformation().Transform(library);

            // Verify the template specialization was translated
            TranslatedTemplateSpecialization specialization = library.FindDeclaration<TranslatedTemplateSpecialization>("MyTemplate<int>");
            TranslatedNormalField field1 = specialization.FindDeclaration<TranslatedNormalField>("SpecializedField1");
            TranslatedNormalField field2 = specialization.FindDeclaration<TranslatedNormalField>("SpecializedField2");
            TranslatedFunction function = specialization.FindDeclaration<TranslatedFunction>("SpecializedMethod");
            TranslatedParameter parameter = function.FindDeclaration<TranslatedParameter>("specializedParameter");
            TranslatedRecord nestedStruct = specialization.FindDeclaration<TranslatedRecord>("SpecializedStruct");
            TranslatedNormalField nestedField = nestedStruct.FindDeclaration<TranslatedNormalField>("SpecializedNestedField");

            static void CheckType(TypeReference type)
            {
                CSharpBuiltinTypeReference cSharpType = Assert.IsType<CSharpBuiltinTypeReference>(type);
                Assert.Equal(CSharpBuiltinType.Int, cSharpType);
            }

            CheckType(field1.Type);
            CheckType(field2.Type);
            CheckType(function.ReturnType);
            CheckType(parameter.Type);
            CheckType(nestedField.Type);

            Assert.Equal(8, specialization.Size);
            Assert.Equal(0, field1.Offset);
            Assert.Equal(4, field2.Offset);

            // Verify none of the "unspecialized" members are present
            Assert.DoesNotContain(specialization, m => m.Name is "Field" or "Method" or "MyStruct");
        }

        [Fact]
        public void ImplicitTemplateSpecialization()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
template<typename T> class MyTemplate
{
public:
    T Field1;
    T Field2;

    T Method(T x)
    {
        return x;
    }
};

typedef MyTemplate<int> MyTemplateInt;
typedef MyTemplate<short> MyTemplateShort;
typedef MyTemplate<char*> MyTemplateCharPtr;

// This function causes Clang to implicitly instantiate MyTemplate<int>, testing that things work in that scenario and not just in the late-instantiation scenario.
int Test(MyTemplate<int>& t)
{
    return t.Field1;
}
",
                targetTriple: "x86_64-pc-win32"
            );

            // Reduce types to make them easier to reason about
            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new CSharpBuiltinTypeTransformation().Transform(library);

            void VerifyTemplate(string typedefName, int typeSize, TypeReference type)
            {
                TranslatedTypedef typedef = library.FindDeclaration<TranslatedTypedef>(typedefName);
                TranslatedTypeReference templateReference = Assert.IsAssignableFrom<TranslatedTypeReference>(typedef.UnderlyingType);
                TranslatedTemplateSpecialization template = Assert.IsAssignableFrom<TranslatedTemplateSpecialization>(templateReference.TryResolve(library));
                Assert.Equal(typeSize * 2, template.Size);

                TranslatedNormalField field1 = template.FindDeclaration<TranslatedNormalField>("Field1");
                TranslatedNormalField field2 = template.FindDeclaration<TranslatedNormalField>("Field2");
                Assert.Equal(0, field1.Offset);
                Assert.Equal(typeSize, field2.Offset);
                Assert.Equal(type, field1.Type);
                Assert.Equal(type, field2.Type);

                TranslatedFunction method = template.FindDeclaration<TranslatedFunction>("Method");
                TranslatedParameter parameter = method.FindDeclaration<TranslatedParameter>("x");
                Assert.Equal(type, method.ReturnType);
                Assert.Equal(type, parameter.Type);

                //TODO: https://github.com/InfectedLibraries/Biohazrd/issues/179
                // TranslatedRecord nestedStruct = template.FindDeclaration<TranslatedRecord>("MyStruct");
                // TranslatedNormalField nestedField = nestedStruct.FindDeclaration<TranslatedNormalField>("NestedField");
                // Assert.Equal(type, nestedField.Type);
            }

            VerifyTemplate("MyTemplateInt", 4, CSharpBuiltinType.Int);
            VerifyTemplate("MyTemplateShort", 2, CSharpBuiltinType.Short);
            VerifyTemplate("MyTemplateCharPtr", 8, new PointerTypeReference(CSharpBuiltinType.Byte));
        }

        [FutureFact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/179")]
        public void ImplicitTemplateSpecialization_NestedRecord()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
template<typename T> class MyTemplate
{
public:
    struct MyStruct
    {
        T NestedField;
    };
};

typedef MyTemplate<int> MyTemplateInt;
typedef MyTemplate<short> MyTemplateShort;
typedef MyTemplate<char*> MyTemplateCharPtr;

// This function causes Clang to implicitly instantiate MyTemplate<int>::MyStruct, testing that things work in that scenario and not just in the late-instantiation scenario.
int Test(MyTemplate<int>::MyStruct& s)
{
    return s.NestedField;
}
",
                targetTriple: "x86_64-pc-win32"
            );

            // Reduce types to make them easier to reason about
            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new CSharpBuiltinTypeTransformation().Transform(library);

            void VerifyTemplate(string typedefName, TypeReference type)
            {
                TranslatedTypedef typedef = library.FindDeclaration<TranslatedTypedef>(typedefName);
                TranslatedTypeReference templateReference = Assert.IsAssignableFrom<TranslatedTypeReference>(typedef.UnderlyingType);
                TranslatedTemplateSpecialization template = Assert.IsAssignableFrom<TranslatedTemplateSpecialization>(templateReference.TryResolve(library));
                TranslatedRecord nestedStruct = template.FindDeclaration<TranslatedRecord>("MyStruct");
                TranslatedNormalField nestedField = nestedStruct.FindDeclaration<TranslatedNormalField>("NestedField");
                Assert.Equal(type, nestedField.Type);
            }

            VerifyTemplate("MyTemplateInt", CSharpBuiltinType.Int);
            VerifyTemplate("MyTemplateShort", CSharpBuiltinType.Short);
            VerifyTemplate("MyTemplateCharPtr", new PointerTypeReference(CSharpBuiltinType.Byte));
        }
    }
}
