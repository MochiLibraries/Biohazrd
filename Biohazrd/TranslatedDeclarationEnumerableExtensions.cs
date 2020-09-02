using System.Collections.Generic;

namespace Biohazrd
{
    public static class TranslatedDeclarationEnumerableExtensions
    {
        public struct RecursiveEnumerable
        {
            private IEnumerable<TranslatedDeclaration> RootEnumerable;

            internal RecursiveEnumerable(IEnumerable<TranslatedDeclaration> rootEnumerable)
                => RootEnumerable = rootEnumerable;

            public RecursiveTranslatedDeclarationEnumerator GetEnumerator()
                => new RecursiveTranslatedDeclarationEnumerator(RootEnumerable.GetEnumerator());
        }

        public static RecursiveEnumerable EnumerateRecursively(this IEnumerable<TranslatedDeclaration> translatedDeclarationCollection)
            => new RecursiveEnumerable(translatedDeclarationCollection);
    }
}
