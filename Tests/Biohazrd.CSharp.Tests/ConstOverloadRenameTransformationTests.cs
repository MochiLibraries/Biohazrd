using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Common;
using Biohazrd.Transformation.Common.Metadata;
using System.Linq;
using Xunit;

namespace Biohazrd.CSharp.Tests
{
    public sealed class ConstOverloadTests : BiohazrdTestBase
    {
        [Fact]
        public void BasicTest()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class ConstOverloads
{
public:
    ConstOverloads& Method();
    ConstOverloads& Method() const;
};"
            );

            library = new ConstOverloadRenameTransformation().Transform(library);

            TranslatedRecord constOverloadsClass = library.FindDeclaration<TranslatedRecord>("ConstOverloads");
            Assert.Equal(2, constOverloadsClass.TotalMemberCount);
            Assert.True(constOverloadsClass.Members.All(m => m is TranslatedFunction));
            {
                TranslatedFunction method1 = (TranslatedFunction)constOverloadsClass.Members[0];
                Assert.Equal("Method", method1.Name);
                Assert.False(method1.IsConst);
                Assert.False(method1.Metadata.Has<HideDeclarationFromCodeCompletion>());
            }
            {
                TranslatedFunction method2 = (TranslatedFunction)constOverloadsClass.Members[1];
                Assert.NotEqual("Method", method2.Name);
                Assert.True(method2.IsConst);
                Assert.True(method2.Metadata.Has<HideDeclarationFromCodeCompletion>());
            }
        }

        [Fact]
        public void SkipsNonConstOverloads()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class ConstOverloads
{
public:
    ConstOverloads& Method() const;
};"
            );

            library = new ConstOverloadRenameTransformation().Transform(library);

            TranslatedRecord constOverloadsClass = library.FindDeclaration<TranslatedRecord>("ConstOverloads");
            Assert.Single(constOverloadsClass);
            Assert.True(constOverloadsClass.Members.All(m => m is TranslatedFunction));
            {
                TranslatedFunction method1 = (TranslatedFunction)constOverloadsClass.Members[0];
                Assert.Equal("Method", method1.Name);
                Assert.True(method1.IsConst);
                Assert.False(method1.Metadata.Has<HideDeclarationFromCodeCompletion>());
            }
        }

        [Fact]
        public void SkipsNonConstOverloads2()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class ConstOverloads
{
public:
    ConstOverloads& Method() const;
    ConstOverloads& Method(int x) const;
};"
            );

            library = new ConstOverloadRenameTransformation().Transform(library);

            TranslatedRecord constOverloadsClass = library.FindDeclaration<TranslatedRecord>("ConstOverloads");
            Assert.Equal(2, constOverloadsClass.TotalMemberCount);
            Assert.True(constOverloadsClass.Members.All(m => m is TranslatedFunction));
            {
                TranslatedFunction method1 = (TranslatedFunction)constOverloadsClass.Members[0];
                Assert.Equal("Method", method1.Name);
                Assert.True(method1.IsConst);
                Assert.False(method1.Metadata.Has<HideDeclarationFromCodeCompletion>());
            }
            {
                TranslatedFunction method2 = (TranslatedFunction)constOverloadsClass.Members[1];
                Assert.Equal("Method", method2.Name);
                Assert.True(method2.IsConst);
                Assert.False(method2.Metadata.Has<HideDeclarationFromCodeCompletion>());
            }
        }

        // ConstOverloadRenameTransformation does not actually try to determine if a const and non-const are truely overloads of eachother.
        [FutureFact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/114")]
        public void SkipsNonConflictingConstOverloads()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class ConstOverloads
{
public:
    ConstOverloads& Method();
    ConstOverloads& Method(int x) const;
};"
            );

            library = new ConstOverloadRenameTransformation().Transform(library);

            TranslatedRecord constOverloadsClass = library.FindDeclaration<TranslatedRecord>("ConstOverloads");
            Assert.Equal(2, constOverloadsClass.TotalMemberCount);
            Assert.True(constOverloadsClass.Members.All(m => m is TranslatedFunction));
            {
                TranslatedFunction method1 = (TranslatedFunction)constOverloadsClass.Members[0];
                Assert.Equal("Method", method1.Name);
                Assert.False(method1.IsConst);
                Assert.False(method1.Metadata.Has<HideDeclarationFromCodeCompletion>());
            }
            {
                TranslatedFunction method2 = (TranslatedFunction)constOverloadsClass.Members[1];
                Assert.Equal("Method", method2.Name);
                Assert.True(method2.IsConst);
                Assert.False(method2.Metadata.Has<HideDeclarationFromCodeCompletion>());
            }
        }
    }
}
