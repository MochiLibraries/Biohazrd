using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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

        //TODO: Thread safety
        private WeakReference<TranslatedLibrary>? DeclarationLookupCacheLibrary = null;
        private Dictionary<Decl, TranslatedDeclaration?>? DeclarationLookupCache = null;
        private Dictionary<Decl, VisitorContext>? DeclarationContextLookupCache = null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvalidateCacheIfStale()
        {
            // Check if this library is the same one the cache corresponds to
            // (Ideally we just don't bring over the cache when this object is cloned, but records don't currently allow this.)
            if (DeclarationLookupCacheLibrary is null || !DeclarationLookupCacheLibrary.TryGetTarget(out TranslatedLibrary? cacheLibrary) || !ReferenceEquals(cacheLibrary, this))
            {
                DeclarationLookupCacheLibrary = new WeakReference<TranslatedLibrary>(this);
                DeclarationLookupCache = null;
                DeclarationContextLookupCache = null;
            }
        }

        public TranslatedDeclaration? TryFindTranslation(Decl declaration)
        {
            // Invalidate the cache if necessary
            InvalidateCacheIfStale();

            // Create a new cache if there is none
            if (DeclarationLookupCache is null)
            { DeclarationLookupCache = new Dictionary<Decl, TranslatedDeclaration?>(); }

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

            // Cache the result and return it
            DeclarationLookupCache.Add(declaration, result);
            return result;
        }

        public TranslatedDeclaration? TryFindTranslation(Decl declaration, out VisitorContext context)
        {
            // Invalidate the cache if necessary
            InvalidateCacheIfStale();

            // Create a new cache if there is none
            if (DeclarationLookupCache is null)
            { DeclarationLookupCache = new Dictionary<Decl, TranslatedDeclaration?>(); }

            if (DeclarationContextLookupCache is null)
            { DeclarationContextLookupCache = new Dictionary<Decl, VisitorContext>(); }

            // Search for the declaration
            TranslatedDeclaration? result = null;
            VisitorContext resultContext = default;

            if (DeclarationLookupCache.TryGetValue(declaration, out result) && DeclarationContextLookupCache.TryGetValue(declaration, out context))
            { return result; }

            foreach ((VisitorContext childContext, TranslatedDeclaration child) in this.EnumerateRecursivelyWithContext())
            {
                if (child.Declaration == declaration)
                {
                    resultContext = childContext;
                    result = child;
                    break;
                }
            }

            // If there is no result, make the context valid
            if (resultContext.IsDefault)
            {
                Debug.Assert(result is null, "There must be context for a non-null result!");
                resultContext = new VisitorContext(this);
            }

            // Cache the results and return them
            DeclarationLookupCache[declaration] = result;
            DeclarationContextLookupCache[declaration] = resultContext;
            context = resultContext;
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
