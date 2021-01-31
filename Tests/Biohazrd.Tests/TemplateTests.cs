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
    }
}
