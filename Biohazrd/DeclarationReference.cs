using ClangSharp;
using System;

namespace Biohazrd
{
    /// <summary>An indirect reference to a <see cref="TranslatedDeclaration"/>.</summary>
    /// <remarks>
    /// The strategy used to resolve this type reference depends on the specific implementation.
    /// It is possible for the resolution to fail in some situations. (EG: When the referenced declaration was removed from the library after this reference was created.)
    ///
    /// For references which are logically types, use <see cref="TranslatedTypeReference"/> instead.
    /// </remarks>
    public sealed record DeclarationReference
    {
        // A translated type reference is not actually constrained to refer to something which can logically be a type, so we re-use it for this purpose.
        // (This type only really exists to disambiguate whether something is a reference to a declaration or a type-like declaration.)
        private readonly TranslatedTypeReference Reference;

        /// <summary>Tries to resolve this declaration reference from the specified library.</summary>
        /// <returns>A <see cref="TranslatedDeclaration"/> if the resolution succeeded, <c>null</c> otherwise.</returns>
        /// <remarks>A <see cref="DeclarationReference"/> is generally only valid for the specific library it is associated with (or a transformed variant of it.)</remarks>
        public TranslatedDeclaration? TryResolve(TranslatedLibrary library)
            => Reference.TryResolve(library);

        /// <summary>Tries to resolve this declaration reference and its context from the specified library.</summary>
        /// <param name="context">The context corresponding to the returned declaration.</param>
        /// <returns>A <see cref="TranslatedDeclaration"/> if the resolution succeeded, <c>null</c> otherwise.</returns>
        /// <remarks>A <see cref="DeclarationReference"/> is generally only valid for the specific library it is associated with (or a transformed variant of it.)</remarks>
        public TranslatedDeclaration? TryResolve(TranslatedLibrary library, out VisitorContext context)
            => Reference.TryResolve(library, out context);

        /// <summary>Creates a declaration reference which resolves to a <see cref="TranslatedDeclaration"/> corresponding to the specified Clang <see cref="Decl"/>.</summary>
        public DeclarationReference(Decl declaration)
            => Reference = TranslatedTypeReference.Create(declaration);

        /// <summary>Creates a declaration reference which resolves to a <see cref="TranslatedDeclaration"/> corresponding to the specified <see cref="DeclarationId"/>.</summary>
        public DeclarationReference(DeclarationId declarationId)
            => Reference = TranslatedTypeReference.Create(declarationId);

        /// <summary>Creates a type reference which resolve to a <see cref="TranslatedDeclaration"/> or a transformed version of it.</summary>
        public DeclarationReference(TranslatedDeclaration declaration)
            => Reference = TranslatedTypeReference.Create(declaration);

        public override string ToString()
            => Reference.ToString();
    }
}
