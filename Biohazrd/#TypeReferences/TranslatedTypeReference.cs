//#define ENABLE_CACHING
using ClangSharp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

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

        public delegate TranslatedDeclaration LookupFunction(TranslatedLibrary library, out VisitorContext context);

        //TODO: Thread safety
        private TranslatedLibrary? CachedLibrary;
        private TranslatedDeclaration? CachedDeclaration;
        private VisitorContext CachedContext;

        public static long CacheHits = 0;

        /// <summary>Tries to resolve this type reference from the specified library.</summary>
        /// <returns>A <see cref="TranslatedDeclaration"/> if the resolution succeeded, <c>null</c> otherwise.</returns>
        /// <remarks>A <see cref="TranslatedTypeReference"/> is generally only valid for the specific library it is associated with (or a transformed variant of it.)</remarks>
        public TranslatedDeclaration? TryResolve(TranslatedLibrary library)
        {
            if (library is null)
            { throw new ArgumentNullException(nameof(library)); }

#if ENABLE_CACHING
            if (ReferenceEquals(library, CachedLibrary))
#else
            if (ReferenceEquals(library, CachedLibrary) && Key is TranslatedDeclaration)
#endif
            {
                Interlocked.Increment(ref CacheHits);
                return CachedDeclaration;
            }

#if ENABLE_CACHING
            CachedLibrary = library;
            CachedDeclaration = null;
            return CachedDeclaration = Key switch
#else
            return Key switch
#endif
            {
                Decl decl => library.TryFindTranslation(decl),
                LookupFunction lookupFunction => lookupFunction(library, out CachedContext),
                TranslatedDeclaration => throw new InvalidOperationException("Eager type references must not be used with other libraries."),
                _ => throw new InvalidOperationException("The type reference is in an invalid state.")
            };
        }

        /// <summary>Tries to resolve this type reference and its context from the specified library.</summary>
        /// <param name="context">The context corresponding to the returned declaration.</param>
        /// <returns>A <see cref="TranslatedDeclaration"/> if the resolution succeeded, <c>null</c> otherwise.</returns>
        /// <remarks>A <see cref="TranslatedTypeReference"/> is generally only valid for the specific library it is associated with (or a transformed variant of it.)</remarks>
        public TranslatedDeclaration? TryResolve(TranslatedLibrary library, out VisitorContext context)
        {
            if (library is null)
            { throw new ArgumentNullException(nameof(library)); }

#if ENABLE_CACHING
            if (ReferenceEquals(library, CachedLibrary))
#else
            if (ReferenceEquals(library, CachedLibrary) && Key is TranslatedDeclaration)
#endif
            {
                Interlocked.Increment(ref CacheHits);
                context = CachedContext;
                return CachedDeclaration;
            }

#if ENABLE_CACHING
            CachedLibrary = library;
            CachedDeclaration = null;
            CachedContext = default;
#endif

            TranslatedDeclaration? ret = Key switch
            {
                Decl decl => library.TryFindTranslation(decl, out context),
                LookupFunction lookupFunction => lookupFunction(library, out context),
                TranslatedDeclaration => throw new InvalidOperationException("Eager type references must not be used with other libraries."),
                _ => throw new InvalidOperationException("The type reference is in an invalid state.")
            };

#if ENABLE_CACHING
            CachedContext = context;
            return CachedDeclaration = ret;
#else
            return ret;
#endif
        }

        public override string ToString()
        {
            StringBuilder builder = new();
            builder.Append("`Ref resolved by ");

            builder.Append(Key switch
            {
                NamedDecl tagDecl => tagDecl.Name,
                Decl decl => decl.ToString(),
                LookupFunction => "lookup function",
                TranslatedDeclaration preResolved => $"pre-resolved to {preResolved.Name}",
                _ => "unknown",
            });

            if (CachedDeclaration is not null && Key is not TranslatedDeclaration)
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
        public TranslatedTypeReference(LookupFunction lookupFunction)
            => Key = lookupFunction;

        /// <summary>Creates an eager type reference which has already been resolved to a specific <see cref="TranslatedDeclaration"/> in a specific <see cref="VisitorContext"/>.</summary>
        /// <remarks>
        /// This type reference will only resolve for the specified library, attempting to resolve it with other libraries will result in
        /// <see cref="InvalidOperationException"/> and invalidation of the reference.
        ///
        /// This constructor is intended to simplify scenarios where you're going to consume the type reference immediately and then discard it. (Such as to reuse existing type formatting infrastructure.)
        /// You should never attach these references to actual declarations.
        /// </remarks>
        // Note: It is somewhat intentional that this constructor cannot be used from a transformation since it should never be attached to a declaration.
        public TranslatedTypeReference(VisitorContext context, TranslatedDeclaration declaration)
        {
            if (context.IsDefault)
            { throw new ArgumentException("The specified context is invalid.", nameof(context)); }

            Key = declaration;
            CachedLibrary = context.Library;
            CachedDeclaration = declaration;
            CachedContext = context;
        }

        // We need a custom GetHashCode and Equals to avoid considering the private cache fields for equality.
        public override int GetHashCode()
            => HashCode.Combine(base.GetHashCode(), Key);

        public virtual bool Equals(TranslatedTypeReference? other)
            => base.Equals(other) && EqualityComparer<object>.Default.Equals(this.Key, other.Key);
    }
}
