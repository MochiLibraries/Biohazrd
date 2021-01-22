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
        public ImmutableArray<TranslatedMacro> Macros { get; }
        /// <summary>An array of <see cref="TranslatedFile"/>s corresponding to the input files originally used to create the original <see cref="TranslatedLibrary"/>.</summary>
        /// <remarks>This will not include <see cref="TranslatedFile.Synthesized"/> even if any declarations are using it.</remarks>
        public ImmutableArray<TranslatedFile> Files { get; }
        public ImmutableArray<TranslationDiagnostic> ParsingDiagnostics { get; init; }

        internal TranslatedLibrary
        (
            TranslationUnitAndIndex translationUnitAndIndex,
            ImmutableArray<TranslatedFile> files,
            ImmutableArray<TranslationDiagnostic> parsingDiagnostics,
            ImmutableList<TranslatedDeclaration> declarations,
            ImmutableArray<TranslatedMacro> macros
        )
        {
            TranslationUnitAndIndex = translationUnitAndIndex;
            Declarations = declarations;
            Macros = macros;
            Files = files;
            ParsingDiagnostics = parsingDiagnostics;
        }

        public IEnumerator<TranslatedDeclaration> GetEnumerator()
            => Declarations.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        //TODO: Thread safety
        private WeakReference<TranslatedLibrary>? DeclarationLookupCacheLibrary = null;
        private Dictionary<Decl, (TranslatedDeclaration?, VisitorContext)> ClangDeclarationLookupCache = new();
        private Dictionary<DeclarationId, (TranslatedDeclaration?, VisitorContext)> DeclarationIdLookupCache = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvalidateCacheIfStale()
        {
            // Check if this library is the same one the cache corresponds to
            // (Ideally we just don't bring over the cache when this object is cloned, but records don't currently allow this.)
            if (DeclarationLookupCacheLibrary is null || !DeclarationLookupCacheLibrary.TryGetTarget(out TranslatedLibrary? cacheLibrary) || !ReferenceEquals(cacheLibrary, this))
            {
                DeclarationLookupCacheLibrary = new WeakReference<TranslatedLibrary>(this);
                ClangDeclarationLookupCache.Clear();
                DeclarationIdLookupCache.Clear();
            }
        }

        public TranslatedDeclaration? TryFindTranslation(Decl declaration)
            // The intent of this overload is for it to be used by consumers which don't need the VisitorContext to avoid the extra allocations that come with it.
            // However, it was determined the savings were pretty minor here so this overload is convienence-only now for the sake of simplicity.
            // https://github.com/InfectedLibraries/Biohazrd/issues/60#issuecomment-698585488
            => TryFindTranslation(declaration, out _);

        public TranslatedDeclaration? TryFindTranslation(Decl declaration, out VisitorContext context)
        {
            // Invalidate the cache if necessary
            InvalidateCacheIfStale();

            // Search for the declaration
            (TranslatedDeclaration? Result, VisitorContext Context) resultWithContext = default;

            if (ClangDeclarationLookupCache.TryGetValue(declaration, out resultWithContext))
            {
                context = resultWithContext.Context;
                return resultWithContext.Result;
            }

            foreach ((VisitorContext childContext, TranslatedDeclaration child) in this.EnumerateRecursivelyWithContext())
            {
                if (child.IsTranslationOf(declaration))
                {
                    resultWithContext = (child, childContext);
                    break;
                }
            }

            // If there is no result, make the context valid
            if (resultWithContext.Context.IsDefault)
            {
                Debug.Assert(resultWithContext.Result is null, "There must be context for a non-null result!");
                resultWithContext.Context = new VisitorContext(this);
            }

            // Cache the results and return them
            ClangDeclarationLookupCache.Add(declaration, resultWithContext);
            context = resultWithContext.Context;
            return resultWithContext.Result;
        }

        public TranslatedDeclaration? TryFindTranslation(DeclarationId id)
            // The intent of this overload is for it to be used by consumers which don't need the VisitorContext to avoid the extra allocations that come with it.
            // However, it was determined the savings were pretty minor here so this overload is convienence-only now for the sake of simplicity.
            // https://github.com/InfectedLibraries/Biohazrd/issues/60#issuecomment-698585488
            => TryFindTranslation(id, out _);

        public TranslatedDeclaration? TryFindTranslation(DeclarationId id, out VisitorContext context)
        {
            // Invalidate the cache if necessary
            InvalidateCacheIfStale();

            // Search for the declaration
            (TranslatedDeclaration? Result, VisitorContext Context) resultWithContext = default;

            if (DeclarationIdLookupCache.TryGetValue(id, out resultWithContext))
            {
                context = resultWithContext.Context;
                return resultWithContext.Result;
            }

            foreach ((VisitorContext childContext, TranslatedDeclaration child) in this.EnumerateRecursivelyWithContext())
            {
                if (child.Id == id || child.ReplacedIds.Contains(id))
                {
                    resultWithContext = (child, childContext);
                    break;
                }
            }

            // If there is no result, make the context valid
            if (resultWithContext.Context.IsDefault)
            {
                Debug.Assert(resultWithContext.Result is null, "There must be context for a non-null result!");
                resultWithContext.Context = new VisitorContext(this);
            }

            // Cache the results and return them
            DeclarationIdLookupCache.Add(id, resultWithContext);
            context = resultWithContext.Context;
            return resultWithContext.Result;
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
