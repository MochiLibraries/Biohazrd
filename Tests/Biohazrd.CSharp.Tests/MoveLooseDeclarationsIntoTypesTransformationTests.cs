using Biohazrd.Tests.Common;
using System.Linq;
using Xunit;

namespace Biohazrd.CSharp.Tests
{
    public sealed class MoveLooseDeclarationsIntoTypesTransformationTests : BiohazrdTestBase
    {
        [Fact]
        public void Basic_NonMemberFunction()
        {
            TranslatedLibrary library = CreateLibrary(@"void Test();");
            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("Test");
            library = new MoveLooseDeclarationsIntoTypesTransformation().Transform(library);
            SynthesizedLooseDeclarationsTypeDeclaration container = library.FindDeclaration<SynthesizedLooseDeclarationsTypeDeclaration>();
            Assert.Equal("A", container.Name); // Transformation uses name of file for default name
            Assert.Contains(function, container);
            Assert.DoesNotContain(function, library);
        }

        [Fact]
        public void Basic_GlobalVariable()
        {
            TranslatedLibrary library = CreateLibrary(@"extern int Test;");
            TranslatedStaticField variable = library.FindDeclaration<TranslatedStaticField>("Test");
            library = new MoveLooseDeclarationsIntoTypesTransformation().Transform(library);
            SynthesizedLooseDeclarationsTypeDeclaration container = library.FindDeclaration<SynthesizedLooseDeclarationsTypeDeclaration>();
            Assert.Equal("A", container.Name); // Transformation uses name of file for default name
            Assert.Contains(variable, container);
            Assert.DoesNotContain(variable, library);
        }

        [Fact]
        public void NothingToDo()
        {
            TranslatedLibrary library = CreateLibrary(@"struct Test { };");
            TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("Test");
            TranslatedLibrary transformed = new MoveLooseDeclarationsIntoTypesTransformation().Transform(library);
            Assert.Contains(record, transformed);
            Assert.DoesNotContain(transformed, d => d is SynthesizedLooseDeclarationsTypeDeclaration);
            Assert.ReferenceEqual(library, transformed);
        }

        [Fact]
        public void TypeWithSameNameAsFile()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct A
{
    void FunctionA();
};

void LooseFunction();
extern int GlobalVariable;
"
            );

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("LooseFunction");
            TranslatedStaticField global = library.FindDeclaration<TranslatedStaticField>("GlobalVariable");

            library = new MoveLooseDeclarationsIntoTypesTransformation().Transform(library);

            Assert.Single(library);
            TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("A");
            Assert.Contains(function, record);
            Assert.Contains(global, record);
            Assert.Contains(record, d => d is TranslatedFunction { Name: "FunctionA" });
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/137")]
        public void Namespaces_Basic()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
namespace Namespace1
{
    void Function1();
}

namespace Namespace2
{
    void Function2();
}
"
            );

            TranslatedFunction function1 = library.FindDeclaration<TranslatedFunction>("Function1");
            TranslatedFunction function2 = library.FindDeclaration<TranslatedFunction>("Function2");

            library = new MoveLooseDeclarationsIntoTypesTransformation().Transform(library);

            Assert.Equal(2, library.Count(d => d is SynthesizedLooseDeclarationsTypeDeclaration));

            SynthesizedLooseDeclarationsTypeDeclaration namespace1Container = library.FindDeclaration<SynthesizedLooseDeclarationsTypeDeclaration>(d => d.Namespace == "Namespace1");
            Assert.Contains(function1, namespace1Container);

            SynthesizedLooseDeclarationsTypeDeclaration namespace2Container = library.FindDeclaration<SynthesizedLooseDeclarationsTypeDeclaration>(d => d.Namespace == "Namespace2");
            Assert.Contains(function2, namespace2Container);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/137")]
        public void Namespaces_MatchingType()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
namespace Namespace1
{
    struct A { };
    void Function1();
}

namespace Namespace2
{
    struct A { };
    void Function2();
}
"
            );

            TranslatedFunction function1 = library.FindDeclaration<TranslatedFunction>("Function1");
            TranslatedFunction function2 = library.FindDeclaration<TranslatedFunction>("Function2");

            library = new MoveLooseDeclarationsIntoTypesTransformation().Transform(library);

            Assert.Equal(2, library.Count(d => d is TranslatedRecord));

            TranslatedRecord namespace1Container = library.FindDeclaration<TranslatedRecord>(d => d.Name == "A" && d.Namespace == "Namespace1");
            Assert.Contains(function1, namespace1Container);

            TranslatedRecord namespace2Container = library.FindDeclaration<TranslatedRecord>(d => d.Name == "A" && d.Namespace == "Namespace2");
            Assert.Contains(function2, namespace2Container);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/137")]
        public void Namespaces_MatchingTypeForOne()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
namespace Namespace1
{
    struct A { };
    void Function1();
}

namespace Namespace2
{
    void Function2();
}
"
            );

            TranslatedFunction function1 = library.FindDeclaration<TranslatedFunction>("Function1");
            TranslatedFunction function2 = library.FindDeclaration<TranslatedFunction>("Function2");

            library = new MoveLooseDeclarationsIntoTypesTransformation().Transform(library);

            Assert.Equal(1, library.Count(d => d is TranslatedRecord));
            Assert.Equal(1, library.Count(d => d is SynthesizedLooseDeclarationsTypeDeclaration));

            TranslatedRecord namespace1Container = library.FindDeclaration<TranslatedRecord>(d => d.Name == "A" && d.Namespace == "Namespace1");
            Assert.Contains(function1, namespace1Container);

            SynthesizedLooseDeclarationsTypeDeclaration namespace2Container = library.FindDeclaration<SynthesizedLooseDeclarationsTypeDeclaration>(d => d.Name == "A" && d.Namespace == "Namespace2");
            Assert.Contains(function2, namespace2Container);
        }
    }
}
