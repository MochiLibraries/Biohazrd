using System;

namespace Biohazrd
{
    /// <summary>The base type for <see cref="TranslatedTypeReference"/> implementations that benefit from caching.</summary>
    /// <remarks>
    /// Type reference consumers should generally not case whether or not a <see cref="TranslatedTypeReference"/> uses caching internally or not.
    ///
    /// Implementations can use <see cref="ToStringSuffix"/> to include the cached result in the <see cref="ToString"/> output for debugging purposes.
    /// </remarks>
    public abstract record CachingTranslatedTypeReference : TranslatedTypeReference
    {
        //TODO: Thread safety
        private TranslatedLibrary? CachedLibrary;
        private TranslatedDeclaration? CachedDeclaration;
        private VisitorContext CachedContext;

        protected abstract TranslatedDeclaration? TryResolveImplementation(TranslatedLibrary library);
        protected abstract TranslatedDeclaration? TryResolveImplementation(TranslatedLibrary library, out VisitorContext context);

        public override sealed TranslatedDeclaration? TryResolve(TranslatedLibrary library)
        {
            if (library is null)
            { throw new ArgumentNullException(nameof(library)); }

            // If there's a cache hit, return the cached value
            if (ReferenceEquals(library, CachedLibrary))
            { return CachedDeclaration; }

            CachedLibrary = library;
            CachedDeclaration = null;
            CachedContext = default;
            return CachedDeclaration = TryResolveImplementation(library);
        }

        public override sealed TranslatedDeclaration? TryResolve(TranslatedLibrary library, out VisitorContext context)
        {
            if (library is null)
            { throw new ArgumentNullException(nameof(library)); }

            // If there's a cache hit, return the cached value
            if (ReferenceEquals(library, CachedLibrary) && !CachedContext.IsDefault)
            {
                context = CachedContext;
                return CachedDeclaration;
            }

            CachedLibrary = library;
            CachedDeclaration = null;
            CachedContext = default;
            TranslatedDeclaration? result = TryResolveImplementation(library, out context);
            CachedContext = context;
            return CachedDeclaration = result;
        }

        protected string ToStringSuffix => CachedDeclaration is not null ? $" ({CachedDeclaration.Name})" : String.Empty;

        public override string ToString()
            => $"`Cached translated type reference{ToStringSuffix}`";

        // The cache should not be part of the equality checks, so we manually implement Equals and GetHashCode to exlude them.
        public virtual bool Equals(CachingTranslatedTypeReference? other)
            => base.Equals(other);

        public override int GetHashCode()
            => base.GetHashCode();
    }
}
