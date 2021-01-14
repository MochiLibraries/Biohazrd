using System.Linq;
using Xunit;

namespace Biohazrd.Tests.Common
{
    public abstract class BiohazrdTestBase
    {
        /// <param name="targetTriple">https://clang.llvm.org/docs/CrossCompilation.html#target-triple</param>
        protected TranslatedLibrary CreateLibrary(string cppCode, string? targetTriple = null, TranslationOptions? options = null)
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = cppCode
            });

            if (targetTriple is not null)
            { builder.AddCommandLineArgument($"--target={targetTriple}"); }

            if (options is not null)
            { builder.Options = options; }

            TranslatedLibrary library = builder.Create();
            Assert.Empty(library.ParsingDiagnostics.Where(d => d.IsError));
            return library;
        }
    }
}
