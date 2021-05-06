namespace Biohazrd
{
    public static class TranslatedDeclarationExtensions
    {
        /// <summary>Creates a clone of this declaration which is unique from this one for purposes of type reference resolution.</summary>
        /// <remarks>
        /// Type references which resolved to this instance will not resolve to the new instance and vice-versa.
        ///
        /// This clone is a shallow clone, so this does not extend to child declarations.
        /// </remarks>
        public static TDeclaration CreateUniqueClone<TDeclaration>(this TDeclaration declaration)
            where TDeclaration : TranslatedDeclaration
            => (TDeclaration)TranslatedDeclaration._CreateUniqueClone(declaration);
    }
}
