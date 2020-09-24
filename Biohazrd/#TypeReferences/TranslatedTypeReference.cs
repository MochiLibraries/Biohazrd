using ClangSharp;
using System;

namespace Biohazrd
{
    /// <summary>A type reference which can resolve to a <see cref="TranslatedDeclaration"/>.</summary>
    /// <remarks>
    /// The strategy used to resolve this type reference depends on the specific implementation.
    /// It is possible for the resolution to fail in some situations. (EG: When the referenced declaration was removed from the library after this reference was created.)
    /// </remarks>
    public abstract record TranslatedTypeReference : TypeReference
    {
        /// <summary>Tries to resolve this type reference from the specified library.</summary>
        /// <returns>A <see cref="TranslatedDeclaration"/> if the resolution succeeded, <c>null</c> otherwise.</returns>
        /// <remarks>A <see cref="TranslatedTypeReference"/> is generally only valid for the specific library it is associated with (or a transformed variant of it.)</remarks>
        public abstract TranslatedDeclaration? TryResolve(TranslatedLibrary library);

        /// <summary>Tries to resolve this type reference and its context from the specified library.</summary>
        /// <param name="context">The context corresponding to the returned declaration.</param>
        /// <returns>A <see cref="TranslatedDeclaration"/> if the resolution succeeded, <c>null</c> otherwise.</returns>
        /// <remarks>A <see cref="TranslatedTypeReference"/> is generally only valid for the specific library it is associated with (or a transformed variant of it.)</remarks>
        public abstract TranslatedDeclaration? TryResolve(TranslatedLibrary library, out VisitorContext context);

        public override string ToString()
            => "`Translated type reference`";

        /// <summary>Creates a type reference which resolves to a <see cref="TranslatedDeclaration"/> corresponding to the specified Clang <see cref="Decl"/>.</summary>
        public static TranslatedTypeReference Create(Decl declaration)
            => new ClangDeclTranslatedTypeReference(declaration);

        /// <summary>Creates a type reference which resolve to a <see cref="TranslatedDeclaration"/> or a transformed version of it.</summary>
        public static TranslatedTypeReference Create(TranslatedDeclaration declaration)
        {
            if (declaration.Declaration is null)
            { throw new ArgumentException("Type references to synthesized declarations are not yet supported.", nameof(declaration)); }

            return new ClangDeclTranslatedTypeReference(declaration.Declaration);
        }
    }
}
