using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using ClangType = ClangSharp.Type;

namespace Biohazrd
{
    public sealed record TranslatedLibrary : IEnumerable<TranslatedDeclaration>
    {
        private readonly TranslationUnitAndIndex TranslationUnitAndIndex;

        public ImmutableList<TranslatedDeclaration> Declarations { get; init; }
        public ImmutableArray<TranslatedFile> Files { get; }
        public ImmutableArray<TranslationDiagnostic> ParsingDiagnostics { get; }

        internal TranslatedLibrary
        (
            TranslationUnitAndIndex translationUnitAndIndex,
            ImmutableArray<TranslatedFile> files,
            ImmutableArray<TranslationDiagnostic> parsingDiagnostics,
            ImmutableList<TranslatedDeclaration> declarations
        )
        {
            TranslationUnitAndIndex = translationUnitAndIndex;
            Declarations = declarations;
            Files = files;
            ParsingDiagnostics = parsingDiagnostics;
        }

        public IEnumerator<TranslatedDeclaration> GetEnumerator()
            => Declarations.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        private WeakReference<TranslatedLibrary>? DeclarationLookupCacheLibrary = null;
        private Dictionary<Decl, TranslatedDeclaration?>? DeclarationLookupCache = null;
        public TranslatedDeclaration? TryFindTranslation(Decl declaration)
        {
            // Check if the cachce is stale
            // (Ideally we just don't bring over the cache when this object is cloned, but 
            if (DeclarationLookupCache is not null)
            {
                if (DeclarationLookupCacheLibrary is null || !DeclarationLookupCacheLibrary.TryGetTarget(out TranslatedLibrary? cacheLibrary) || !ReferenceEquals(cacheLibrary, this))
                { DeclarationLookupCache = null; }
            }

            // Create a new cache if there is none
            if (DeclarationLookupCache is null)
            {
                DeclarationLookupCacheLibrary = new WeakReference<TranslatedLibrary>(this);
                DeclarationLookupCache = new Dictionary<Decl, TranslatedDeclaration?>();
            }

            // Search for the declaration
            TranslatedDeclaration? result = null;

            if (DeclarationLookupCache.TryGetValue(declaration, out result))
            { return result; }

            foreach (TranslatedDeclaration child in this.EnumerateRecursively())
            {
                if (child.Declaration == declaration)
                {
                    result = child;
                    break;
                }
            }

            DeclarationLookupCache.Add(declaration, result);
            return result;
        }

        /// <summary>Finds the ClangSharp <see cref="Cursor"/> for the given <see cref="CXCursor"/> handle.</summary>
        /// <remarks>
        /// The provided cursor handle must be valid, non-null, and come from the same translation unit as the one used by this library.
        ///
        /// This method is provided for advanced scenarios only.
        /// Typically you should not need it unless you're accessing Clang information that ClangSharp doesn't expose in a clean manner.
        /// </remarks>
        public Cursor FindClangCursor(CXCursor handle)
            => TranslationUnitAndIndex.TranslationUnit.FindCursor(handle);

        /// <summary>Finds the ClangSharp <see cref="ClangType"/> for the given <see cref="CXType"/> handle.</summary>
        /// <remarks>
        /// The provided type handle must be valid, and come from the same translation unit as the one used by this library.
        ///
        /// This method is provided for advanced scenarios only.
        /// Typically you should not need it unless you're accessing Clang information that ClangSharp doesn't expose in a clean manner.
        /// </remarks>
        public ClangType FindClangType(CXType handle)
            => TranslationUnitAndIndex.TranslationUnit.FindType(handle);
    }
}
