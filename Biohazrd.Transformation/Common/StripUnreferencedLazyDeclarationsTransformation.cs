using Biohazrd.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Biohazrd.Transformation.Common
{
    /// <summary>Strips any declarations marked with <see cref="LazilyGenerated"/> which are not referenced.</summary>
    public sealed class StripUnreferencedLazyDeclarationsTransformation : TransformationBase
    {
        private readonly HashSet<TranslatedDeclaration> ReferencedLazyDeclarations = new(ReferenceEqualityComparer.Instance);
        private readonly ReferenceWalker Walker = new();

        private sealed class ReferenceWalker : __TypeReferenceVisitor
        {
            // We're really using this more like HashSet<(TranslatedDeclaration, VisitorContext)>, but we want to use reference equality on the declaration and tuples don't make that easy.
            private Dictionary<TranslatedDeclaration, VisitorContext> FoundLazyDeclarations = new(ReferenceEqualityComparer.Instance);

            public int Count => FoundLazyDeclarations.Count;

            public void Clear()
                => FoundLazyDeclarations.Clear();

            public Dictionary<TranslatedDeclaration, VisitorContext> GetFoundLazyDeclarationsAndClear()
            {
                Dictionary<TranslatedDeclaration, VisitorContext> result = FoundLazyDeclarations;
                FoundLazyDeclarations = new Dictionary<TranslatedDeclaration, VisitorContext>(ReferenceEqualityComparer.Instance);
                return result;
            }

            protected override void VisitRecord(VisitorContext context, TranslatedRecord declaration)
                => VisitRecord(context, declaration, isDirectVisit: false);

            public void VisitDirect(VisitorContext context, TranslatedDeclaration declaration)
            {
                if (declaration is TranslatedRecord record)
                {
                    Debug.Assert(declaration.Metadata.Has<LazilyGenerated>());
                    VisitRecord(context, record, isDirectVisit: true);
                }
                else
                { base.Visit(context, declaration); }
            }

            private void VisitRecord(VisitorContext context, TranslatedRecord declaration, bool isDirectVisit)
            {
                // If this isn't a direct visit and the declaration is lazy, skip it to avoid counting a lazy declaration as referenced when it is referenced by
                // its self or another lazy declaration (If this is a direct visit, we know this lazy declaration has already been referenced.)
                if (!isDirectVisit && declaration.Metadata.Has<LazilyGenerated>())
                { return; }

                base.VisitRecord(context, declaration);
            }

            protected override void VisitTypeReference(VisitorContext context, ImmutableArray<TypeReference> parentTypeReferences, TypeReference typeReference)
            {
                if (typeReference is not TranslatedTypeReference translatedTypeReference)
                { goto baseCall; }

                VisitorContext resolvedContext;
                TranslatedDeclaration? resolvedDeclaration = translatedTypeReference.TryResolve(context.Library, out resolvedContext);

                if (resolvedDeclaration is null)
                { goto baseCall; }

                // Check if the resolved declaration or any of its parental lineage are lazily generated and mark them as found
                while (true)
                {
                    if (resolvedDeclaration.Metadata.Has<LazilyGenerated>())
                    { FoundLazyDeclarations.TryAdd(resolvedDeclaration, resolvedContext); }

                    resolvedDeclaration = resolvedContext.ParentDeclaration;
                    if (resolvedDeclaration is null)
                    { break; }

                    resolvedContext = resolvedContext.MakePrevious();
                }

baseCall:
                base.VisitTypeReference(context, parentTypeReferences, typeReference);
            }
        }

        protected override TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
        {
            Debug.Assert(ReferencedLazyDeclarations.Count == 0);
            ReferencedLazyDeclarations.Clear();
            Debug.Assert(Walker.Count == 0);
            Walker.Clear();

            // Enumerate all lazy declarations referenced by non-lazy declarations in the library
            Walker.Visit(library);

            // Loop so we keep adding new lazy declarations found to be referenced by lazy declarations processed by the previous iteration
            while (Walker.Count > 0)
            {
                // Reset the walker and process its results
                foreach ((TranslatedDeclaration lazyDeclaration, VisitorContext lazyContext) in Walker.GetFoundLazyDeclarationsAndClear())
                {
                    // Try to record the referenced lazy declaration. If it was already seen before, there's nothing more to do
                    if (!ReferencedLazyDeclarations.Add(lazyDeclaration))
                    { continue; }

                    // If this is the first time we've seen this lazy declaration, walk it to find any lazy declarations which it references
                    Walker.VisitDirect(lazyContext, lazyDeclaration);
                }
            }

            return library;
        }

        protected override TransformationResult TransformRecord(TransformationContext context, TranslatedRecord declaration)
        {
            if (!declaration.Metadata.Has<LazilyGenerated>())
            { return declaration; }

            if (ReferencedLazyDeclarations.Contains(declaration))
            { return declaration; }

            // This lazily-generated declaration was not referenced, remove it
            return null;
        }

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
        {
            ReferencedLazyDeclarations.Clear();
            return library;
        }
    }
}
