using Xunit;

namespace Biohazrd.Tests.Common
{
    public abstract class BiohazrdTestBase
    {
        protected TranslatedLibrary CreateLibrary(string cppCode, string? targetTriple = null)
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = cppCode
            });

            if (targetTriple is not null)
            { builder.AddCommandLineArgument($"--target={targetTriple}"); }

            TranslatedLibrary library = builder.Create();
            Assert.Empty(library.ParsingDiagnostics);
            return library;
        }
    }
}
