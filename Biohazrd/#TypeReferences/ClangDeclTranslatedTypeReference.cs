using ClangSharp;

namespace Biohazrd
{
    internal sealed record ClangDeclTranslatedTypeReference : CachingTranslatedTypeReference
    {
        private readonly Decl ClangDecl;

        internal ClangDeclTranslatedTypeReference(Decl clangDecl)
            => ClangDecl = clangDecl;

        protected override TranslatedDeclaration? TryResolveImplementation(TranslatedLibrary library)
            => library.TryFindTranslation(ClangDecl);

        protected override TranslatedDeclaration? TryResolveImplementation(TranslatedLibrary library, out VisitorContext context)
            => library.TryFindTranslation(ClangDecl, out context);

        public override string ToString()
            => $"`Ref resolved by {ClangDecl}{ToStringSuffix}`";

        internal override bool __HACK__CouldResolveTo(TranslatedDeclaration declaration)
            => declaration.IsTranslationOf(ClangDecl);
    }
}
