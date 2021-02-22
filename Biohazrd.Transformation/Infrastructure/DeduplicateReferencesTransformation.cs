using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace Biohazrd.Transformation.Infrastructure
{
    /// <summary>De-duplicates .NET object references within each declaration</summary>
    /// <remarks>
    /// This transformation aids in avoiding certain complexities that can arise when having to deal with duplicate declarations within a declaration tree.
    ///
    /// You generally do not ever need to use this transformation.
    /// </remarks>
    public sealed class DeduplicateReferencesTransformation : TransformationBase
    {
        // declaration => declaration, used as a concurrent HashSet
        private ConcurrentDictionary<TranslatedDeclaration, TranslatedDeclaration> FoundReferences = new(ReferenceEqualityComparer.Instance);

        protected override TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
        {
            Debug.Assert(FoundReferences.Count == 0);
            FoundReferences.Clear();
            return library;
        }

        protected override TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
        {
            if (!FoundReferences.TryAdd(declaration, declaration))
            {
                // We've seen this reference before, clone this declaration
                // (No need to log this clone since we know it's unique.)
                TranslatedDeclaration clone = declaration with { };
                Debug.Assert(!ReferenceEquals(clone, declaration));
                return clone;
            }

            return declaration;
        }

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
        {
            FoundReferences.Clear();
            return library;
        }
    }
}
