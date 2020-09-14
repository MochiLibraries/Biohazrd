using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

namespace Biohazrd.Transformation.Common
{
    /// <summary>This transformation removes all declarations with any error diagnostics attached to them and places them in <see cref="BrokenDeclarations"/>.</summary>
    /// <remarks>
    /// <see cref="BrokenDeclarations"/> is not reset between transforms, meaning this transformation
    /// can be used more than once to remove broken declarations at different points in the pipeline.
    /// </remarks>
    public sealed class BrokenDeclarationExtractor : TransformationBase
    {
        private readonly ConcurrentBag<TranslatedDeclaration> _BrokenDeclarations = new();

        private ImmutableArray<TranslatedDeclaration> BrokenDeclarationsCached;
        public ImmutableArray<TranslatedDeclaration> BrokenDeclarations
        {
            get
            {
                // Enumerating ConcurrentBag causes the entire bag to be converted into an array anyway in order to provide the point-in-time snapshot of the collection.
                // As such, it's just as heavy for us to just convert it to an ImmutableArray whenever it's requested and avoided unecessary mass allocation whenever the bag
                // is iterated over more than once.
                // This makes the assumption that elements are only ever added to the bag, never removed.
                if (BrokenDeclarationsCached.IsDefault || BrokenDeclarationsCached.Length != _BrokenDeclarations.Count)
                { BrokenDeclarationsCached = _BrokenDeclarations.ToImmutableArray(); }

                return BrokenDeclarationsCached;
            }
        }

        protected override TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
        {
            // Remove any declarations which have errors
            if (declaration.Diagnostics.Any(d => d.IsError))
            {
                _BrokenDeclarations.Add(declaration);
                return null;
            }

            return declaration;
        }
    }
}
