using Biohazrd.Tests.Common;
using System.Linq;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class ConstOverloadTests : BiohazrdTestBase
    {
        [Fact]
        public void BiohazrdIdentifiesConstOverloads()
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

            TranslatedRecord constOverloadsClass = library.FindDeclaration<TranslatedRecord>("ConstOverloads");
            Assert.Equal(2, constOverloadsClass.TotalMemberCount);
            Assert.True(constOverloadsClass.Members.All(m => m is TranslatedFunction));
            {
                TranslatedFunction method1 = (TranslatedFunction)constOverloadsClass.Members[0];
                Assert.Equal("Method", method1.Name);
                Assert.False(method1.IsConst);
            }
            {
                TranslatedFunction method2 = (TranslatedFunction)constOverloadsClass.Members[1];
                Assert.Equal("Method", method2.Name);
                Assert.True(method2.IsConst);
            }
        }
    }
}
