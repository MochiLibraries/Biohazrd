using Biohazrd.Tests.Common;
using Xunit;

namespace Biohazrd.Tests
{
    // We don't normally have to worry much about name mangling because Clang handles most of it.
    // However there are some edge cases worth testing, hence this file.
    public sealed class NameManglingTests : BiohazrdTestBase
    {
        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/12")]
        public void CppDestructor_MicrosoftABI()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct MyStruct
{
    ~MyStruct();
};
",
                targetTriple: "x86_64-pc-win32"
            );

            TranslatedRecord myStruct = library.FindDeclaration<TranslatedRecord>("MyStruct");
            TranslatedFunction destructor = myStruct.FindDeclaration<TranslatedFunction>();

            // If the mangling is ??_DMyStruct@@QEAAXXZ, then we're incorrectly using the Dtor_Complete (vbase destructor) mangling, which is not what gets exported by MSVC
            Assert.Equal("??1MyStruct@@QEAA@XZ", destructor.MangledName);
        }
    }
}
