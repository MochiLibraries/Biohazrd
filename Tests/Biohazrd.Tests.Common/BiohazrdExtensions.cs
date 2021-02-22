using System;
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

        public static TranslatedDeclaration FindDeclaration(this IEnumerable<TranslatedDeclaration> declarations, Func<TranslatedDeclaration, bool> predicate)
        {
            TranslatedDeclaration? result = declarations.FirstOrDefault(predicate);
            Assert.NotNull(result);
            return result;
        }

        public static TranslatedDeclaration FindDeclaration(this IEnumerable<TranslatedDeclaration> declarations, string declarationName)
            => declarations.FindDeclaration(d => d.Name == declarationName);

        public static TDeclaration FindDeclaration<TDeclaration>(this IEnumerable<TranslatedDeclaration> declarations, Func<TDeclaration, bool> predicate)
            where TDeclaration : TranslatedDeclaration
        {
            TranslatedDeclaration? result = declarations.OfType<TDeclaration>().FirstOrDefault(predicate);
            Assert.NotNull(result);
            return (TDeclaration)result;
        }

        public static TDeclaration FindDeclaration<TDeclaration>(this IEnumerable<TranslatedDeclaration> declarations, string declarationName)
            where TDeclaration : TranslatedDeclaration
            => declarations.FindDeclaration<TDeclaration>(d => d.Name == declarationName);

        public static TDeclaration FindDeclaration<TDeclaration>(this IEnumerable<TranslatedDeclaration> declarations)
            where TDeclaration : TranslatedDeclaration
        {
            TDeclaration? result = declarations.OfType<TDeclaration>().FirstOrDefault();
            Assert.NotNull(result);
            return result;
        }
    }
}
