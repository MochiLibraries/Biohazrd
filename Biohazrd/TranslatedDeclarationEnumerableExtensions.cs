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

        /// <summary>Recursively enumerates the specified declaration collection.</summary>
        /// <remarks>This recursive enumeration is generally allocation-free as long as the depth of the declaration tree isn't too deep.</remarks>
        public static RecursiveEnumerable EnumerateRecursively(this IEnumerable<TranslatedDeclaration> translatedDeclarationCollection)
            => new RecursiveEnumerable(translatedDeclarationCollection);

        public struct ContextualRecursiveEnumerable
        {
            private IEnumerable<TranslatedDeclaration> RootEnumerable;
            private VisitorContext RootContext;

            internal ContextualRecursiveEnumerable(IEnumerable<TranslatedDeclaration> rootEnumerable, VisitorContext rootContext)
            {
                RootEnumerable = rootEnumerable;
                RootContext = rootContext;
            }

            internal ContextualRecursiveEnumerable(TranslatedLibrary library)
            {
                RootEnumerable = library;
                RootContext = new VisitorContext(library);
            }

            public ContextualRecursiveTranslatedDeclarationEnumerator GetEnumerator()
                => new ContextualRecursiveTranslatedDeclarationEnumerator(RootEnumerable.GetEnumerator(), RootContext);
        }

        /// <summary>Recursively enumerates this declaration in the given context.</summary>
        /// <remarks>
        /// This overload requires you have a context corresponding to <paramref name="translatedDeclarationCollection"/>.
        ///
        /// Unlike <see cref="EnumerateRecursively"/>, this method is not generally allocation-free due to the <see cref="VisitorContext"/>.
        /// If you do not need declaration context, use <see cref="EnumerateRecursively"/> instead.
        /// </remarks>
        public static ContextualRecursiveEnumerable EnumerateRecursivelyWithContext(this IEnumerable<TranslatedDeclaration> translatedDeclarationCollection, VisitorContext context)
            => new ContextualRecursiveEnumerable(translatedDeclarationCollection, context);

        /// <summary>Recursively enumerates this library with context.</summary>
        /// <remarks>
        /// Unlike <see cref="EnumerateRecursively"/>, this method is never allocation-free due to the <see cref="VisitorContext"/>.
        /// If you do not need declaration context, use <see cref="EnumerateRecursively"/> instead.
        /// </remarks>
        public static ContextualRecursiveEnumerable EnumerateRecursivelyWithContext(this TranslatedLibrary library)
            => new ContextualRecursiveEnumerable(library);
    }
}
