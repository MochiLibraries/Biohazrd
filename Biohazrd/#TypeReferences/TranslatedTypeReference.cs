using ClangSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biohazrd
{
    /// <summary>A type reference which can resolve to a <see cref="TranslatedDeclaration"/>.</summary>
    /// <remarks>
    /// The strategy used to resolve this type reference depends on how it was constructed.
    /// It is possible for the resolution to fail in some situations. (EG: When the referenced declaration was removed from the library after this reference was created.)
    /// </remarks>
    public record TranslatedTypeReference : TypeReference
    {
        private object Key { get; init; }

        private TranslatedLibrary? CachedLibrary;
        private TranslatedDeclaration? CachedDeclaration;

        /// <summary>Tries to resolve this type reference in the specified library.</summary>
        /// <returns>An <see cref="TranslatedDeclaration"/> if the resolution succeeded, <c>null</c> otherwise.</returns>
        /// <remarks>A <see cref="TranslatedTypeReference"/> is generally only valid for the specific library it is associated with (or a transformed variant of it.)</remarks>
        public TranslatedDeclaration? TryResolve(TranslatedLibrary library)
        {
            if (library is null)
            { throw new ArgumentNullException(nameof(library)); }

            if (ReferenceEquals(library, CachedLibrary))
            { return CachedDeclaration; }

            CachedLibrary = library;
            CachedDeclaration = null;
            return CachedDeclaration = Key switch
            {
                Decl decl => library.TryFindTranslation(decl),
                Func<TranslatedLibrary, TranslatedDeclaration> lookupFunction => lookupFunction(library),
                _ => throw new InvalidOperationException("The type reference is in an invalid state.")
            };
        }

        public override string ToString()
        {
            StringBuilder builder = new();
            builder.Append("`Ref resolved by ");

            builder.Append(Key switch
            {
                NamedDecl tagDecl => tagDecl.Name,
                Decl decl => decl.ToString(),
                Func<TranslatedLibrary, TranslatedDeclaration> _ => "lookup function",
                _ => "unknown",
            });

            if (CachedDeclaration is not null)
            { builder.Append($" ({CachedDeclaration.Name})"); }

            builder.Append('`');
            return builder.ToString();
        }

        /// <summary>Creates a type reference which resolves to a <see cref="TranslatedDeclaration"/> corresponding to the specified Clang <see cref="Decl"/>.</summary>
        public TranslatedTypeReference(Decl clangDeclaration)
            => Key = clangDeclaration;

        /// <summary>Creates a type reference which is resolved using the specified lookup function.</summary>
        /// <remarks>
        /// <paramref name="lookupFunction"/> is invoked lazily when <see cref="TryResolve(TranslatedLibrary)"/> is called, and may be invoked multiple times with different libraries.
        /// 
        /// The function should return <c>null</c> when the resolution fails.
        /// </remarks>
        public TranslatedTypeReference(Func<TranslatedLibrary, TranslatedDeclaration?> lookupFunction)
            => Key = lookupFunction;

        // We need a custom GetHashCode and Equals to avoid considering the private cache fields for equality.
        public override int GetHashCode()
            => HashCode.Combine(base.GetHashCode(), Key);

        public virtual bool Equals(TranslatedTypeReference? other)
            => base.Equals(other) && EqualityComparer<object>.Default.Equals(this.Key, other.Key);
    }
}
