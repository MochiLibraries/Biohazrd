using ClangSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Biohazrd
{
    public sealed record TranslatedLibrary : IDisposable, IEnumerable<TranslatedDeclaration>
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

        public void Dispose()
            => TranslationUnitAndIndex?.Dispose();
    }
}
