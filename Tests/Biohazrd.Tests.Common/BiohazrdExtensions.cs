using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Biohazrd.Tests.Common
{
    public static class BiohazrdExtensions
    {
        public static TranslatedFile FindFile(this TranslatedLibrary library, string fileName)
        {
            TranslatedFile? result = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == fileName);
            Assert.NotNull(result);
            return result;
        }

        public static TranslatedDeclaration FindDeclaration(this IEnumerable<TranslatedDeclaration> declarations, string declarationName)
        {
            TranslatedDeclaration? result = declarations.FirstOrDefault(d => d.Name == declarationName);
            Assert.NotNull(result);
            return result;
        }

        public static TDeclaration FindDeclaration<TDeclaration>(this IEnumerable<TranslatedDeclaration> declarations, string declarationName)
            where TDeclaration : TranslatedDeclaration
        {
            TranslatedDeclaration result = declarations.FindDeclaration(declarationName);
            Assert.IsType<TDeclaration>(result);
            return (TDeclaration)result;
        }

        public static TDeclaration FindDeclaration<TDeclaration>(this IEnumerable<TranslatedDeclaration> declarations)
            where TDeclaration : TranslatedDeclaration
        {
            TDeclaration? result = declarations.OfType<TDeclaration>().FirstOrDefault();
            Assert.NotNull(result);
            return result;
        }
    }
}
