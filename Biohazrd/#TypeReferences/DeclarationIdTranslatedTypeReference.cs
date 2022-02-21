namespace Biohazrd
{
    internal sealed record DeclarationIdTranslatedTypeReference : CachingTranslatedTypeReference
    {
        private readonly DeclarationId Id;

        internal DeclarationIdTranslatedTypeReference(DeclarationId id)
            => Id = id;

        protected override TranslatedDeclaration? TryResolveImplementation(TranslatedLibrary library)
            => library.TryFindTranslation(Id);

        protected override TranslatedDeclaration? TryResolveImplementation(TranslatedLibrary library, out VisitorContext context)
            => library.TryFindTranslation(Id, out context);

        public override string ToString()
            => $"`Ref resolved by {Id}{ToStringSuffix}`";

        internal override bool __HACK__CouldResolveTo(TranslatedDeclaration declaration)
            => declaration.MatchesId(Id);
    }
}
