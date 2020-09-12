using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Biohazrd.Transformation.Infrastructure
{
    /// <summary>Provides lazy <see cref="ImmutableArray{TypeReference}"/> building for <see cref="TypeTransformationBase"/>.</summary>
    /// <remarks>
    /// This type is used to construct a <see cref="ImmutableArray{TypeReference}"/> when its contents might match an existing one.
    ///
    /// Use the <see cref="Add(TypeTransformationResult)"/> method to add the result of a transformation.
    /// If the sequence of adds would've resulted in an identical collection, no new collection will be constructed and no additional memory will be allocated.
    ///
    /// Essentially this type is a lazyily-created <see cref="ImmutableArray{TypeReference}.Builder"/>.
    ///
    /// Diagnostics emitted by the added transformations are accumulated in the assocaited <see cref="DiagnosticAccumulator"/>.
    /// </remarks>
    public ref struct TypeArrayTransformHelper
    {
        private readonly ImmutableArray<TypeReference> Original;
        private ImmutableArray<TypeReference>.Builder? Builder;
        private DiagnosticAccumulatorRef Diagnostics;
        private int LastGoodIndex;
        private bool IsFinished;
        private bool MoveToImmutableCalled;

        /// <summary>Indicates whether the additions have result in a modified collection.</summary>
        public bool WasChanged => Builder is not null;

        /// <summary>True when all elements of the original collection have been transformed.</summary>
        /// <remarks>Unlike with declaration transformations, transformations of type collections cannot be truncated.</remarks>
        public bool TransformationIsComplete
        {
            get
            {
                if (Builder is null)
                { return LastGoodIndex == (Original.Length - 1); }
                else
                { return Builder.Count == Original.Length; }
            }
        }

        public TypeArrayTransformHelper(ImmutableArray<TypeReference> original, ref DiagnosticAccumulator diagnosticAccumulator)
        {
            Original = original;
            Builder = null;
            Diagnostics = new DiagnosticAccumulatorRef(ref diagnosticAccumulator);
            LastGoodIndex = -1;
            IsFinished = false;
            MoveToImmutableCalled = false;
        }

        private ImmutableArray<TypeReference>.Builder CreateBuilder()
        {
            Debug.Assert(Builder is null, "This method should not be called when we already have a builder.");
            ImmutableArray<TypeReference>.Builder builder = ImmutableArray.CreateBuilder<TypeReference>(Original.Length);

            // Add all of the unchanged type references that we skipped earlier
            int i = 0;
            foreach (TypeReference type in Original)
            {
                if (i > LastGoodIndex)
                { break; }

                builder.Add(type);
                i++;
            }

            return builder;
        }

        private bool CheckIsChange(TypeTransformationResult transformation)
        {
            Debug.Assert(Builder is null, "This method should not be called when the collection has already been changed.");

            int i = LastGoodIndex + 1;

            if (i >= Original.Length)
            { throw new InvalidOperationException("The capacity of the original collection was exceeded."); }

            if (Original[i] == transformation.TypeReference)
            {
                // If the types are equals, no change needed
                // Advance LastGoodIndex to indicate that elements up to this index are good
                LastGoodIndex++;
                return false;
            }
            else
            {
                // Equality check failed, the collection is being changed.
                return true;
            }
        }

        public void Add(TypeTransformationResult transformation)
        {
            if (IsFinished)
            { throw new InvalidOperationException("Can't add to an collection once it has been finished."); }

            // If the transformation has diagnostics, accumulate them
            if (transformation.Diagnostics.Length > 0)
            { Diagnostics.AddRange(transformation.Diagnostics); }

            // If the builder was already created, just add the type
            if (Builder is not null)
            {
                if (Builder.Count == Builder.Capacity)
                { throw new InvalidOperationException("The capacity of the original collection was exceeded."); }

                Builder.Add(transformation.TypeReference);
            }
            // If this addition results in a change, create the builder and add the type
            else if (CheckIsChange(transformation))
            {
                Builder = CreateBuilder();
                Builder.Add(transformation.TypeReference);
            }
        }

        public void Finish()
        {
            // If we've already been finished, there's nothing to do.
            if (IsFinished)
            { return; }

            // Mark this instance as finished to prevent further additions
            IsFinished = true;

            // If the collection is being truncated, fail
            // (Type transformations can't result in truncated collections.)
            if (!TransformationIsComplete)
            { throw new InvalidOperationException("Collection transformation was marked as finished before all of the types were transformed."); }
        }

        public ImmutableArray<TypeReference> MoveToImmutable()
        {
            if (MoveToImmutableCalled)
            { throw new InvalidOperationException("This method can only be called once."); }

            MoveToImmutableCalled = true;
            Finish();
            return Builder is not null ? Builder.MoveToImmutable() : Original;
        }
    }
}
