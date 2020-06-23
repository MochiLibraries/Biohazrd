using System;

namespace ClangSharpTest2020
{
    public enum AccessModifier
    {
        Private,
        Protected,
        ProtectedAndInternal,
        ProtectedOrInternal,
        Internal,
        Public
    }

    public static class AccessModifierEx
    {
        public static string ToCSharpKeyword(this AccessModifier modifier)
            => modifier switch
            {
                AccessModifier.Private => "private",
                AccessModifier.Protected => "protected",
                AccessModifier.Internal => "internal",
                AccessModifier.ProtectedOrInternal => "protected internal",
                AccessModifier.ProtectedAndInternal => "private protected",
                AccessModifier.Public => "public",
                _ => throw new ArgumentException("Invalid access modifier specified.", nameof(modifier))
            };

        /// <remarks>
        /// This method helps avoid CS1527: Elements defined in a namespace cannot be explicitly declared as private, protected, protected internal, or private protected.
        /// 
        /// It also applies to elements defined at file scope.
        /// </remarks>
        public static bool IsAllowedInNamespaceScope(this AccessModifier modifier)
            => modifier == AccessModifier.Internal || modifier == AccessModifier.Public;
    }
}
