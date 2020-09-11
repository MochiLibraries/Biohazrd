using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Biohazrd.Transformation.Infrastructure
{
    /// <summary>Provides lazy <see cref="ImmutableArray{TDeclaration}"/> building for <see cref="TransformationBase"/>.</summary>
    /// <remarks>
    /// This type is used to construct a <see cref="ImmutableArray{TDeclaration}"/> when its contents might match an existing one.
    ///
    /// Unlike with <see cref="ListTransformHelper"/>, there is no non-generic equivalent for this type.
    ///
    /// Use the <see cref="Add(TransformationResult)"/> method to add the result of a transformation.
    /// If the sequence of adds would've resulted in an identical collection, no new collection will be constructed and no additional memory will be allocated.
    ///
    /// Essentially this type is a lazyily-created <see cref="ImmutableArray{TDeclaration}.Builder"/>.
    ///
    /// Consumers of this type are expected to use <see cref="HasOtherDeclarations"/> and <see cref="GetOtherDeclarations"/> to handle the case where
    /// a transformation yields declarations of an unexpected type.
    /// </remarks>
    public ref struct ArrayTransformHelper<TDeclaration>
        where TDeclaration : TranslatedDeclaration
    {
        private readonly ImmutableArray<TDeclaration> Original;
        private ImmutableArray<TDeclaration>.Builder? Builder;
        private ImmutableArray<TranslatedDeclaration>.Builder? OtherDeclarationsBuilder;
        private ImmutableArray<TDeclaration>.Enumerator Enumerator;
        private int LastGoodIndex;
        private bool IsFinished;
        private bool MoveToImmutableCalled;
        private bool GetOtherDeclarationsCalled;

        /// <summary>Indicates whether the additions have resulted in a modified collection yet.</summary>
        /// <remarks>Note that this will not indicate a truncated collection until <see cref="Finish"/> is called.</remarks>
        public bool WasChanged => Builder is not null;

        /// <summary>Indicates if an attempt was made to add declarations to this collection which weren't of type <typeparamref name="TDeclaration"/>.</summary>
        /// <remarks>This value is always <code>false</code> when <see cref="WasChanged"/> is <code>false</code>.</remarks>
        public bool HasOtherDeclarations => OtherDeclarationsBuilder is not null;

        public ArrayTransformHelper(ImmutableArray<TDeclaration> original)
        {
            Original = original;
            Builder = null;
            OtherDeclarationsBuilder = null;
            Enumerator = Original.GetEnumerator();
            LastGoodIndex = -1;
            IsFinished = false;
            MoveToImmutableCalled = false;
            GetOtherDeclarationsCalled = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyToBuilder(TranslatedDeclaration declaration)
        {
            if (declaration is TDeclaration tDeclaration)
            { Builder!.Add(tDeclaration); }
            else
            {
                if (OtherDeclarationsBuilder is null)
                { OtherDeclarationsBuilder = ImmutableArray.CreateBuilder<TranslatedDeclaration>(); }

                OtherDeclarationsBuilder.Add(declaration);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyToBuilder(TransformationResult transformation)
        {
            switch (transformation.Count)
            {
                case 1:
                    ApplyToBuilder(transformation.SingleDeclaration);
                    return;
                case > 1:
                    foreach (TranslatedDeclaration declaration in transformation.Declarations)
                    { ApplyToBuilder(declaration); }
                    return;
            }
        }

        private void CreateBuilder(TransformationResult transformation)
        {
            // The initial capacity will be the same as the original to avoid excessive resizing of the builder in the common case where each element has one replacement.
            int initialCapacity = Original.Length;

            // If the transformation that caused the builder to be created was a deletion, remove that from the initial capacity.
            // This optimizes for the case where only one deletion occurs and every other member is left unchanged (or replaced by a single replacement.)
            if (transformation.Count == 0)
            { initialCapacity--; }

            CreateBuilder(transformation, initialCapacity);
        }

        private void CreateBuilder(TransformationResult transformation, int initialCapacity)
        {
            Debug.Assert(initialCapacity >= LastGoodIndex + 1, "The initial capacity should always be large enough to fit the elements which will initially be added.");
            Builder = ImmutableArray.CreateBuilder<TDeclaration>(initialCapacity);

            // Add all of the unchanged declarations earlier that we skipped
            // (Note: Since we explicitly set the capacity earlier, there's no reason to make this use AddRange.)
            int i = 0;
            foreach (TDeclaration declaration in Original)
            {
                if (i > LastGoodIndex)
                { break; }

                Builder.Add(declaration);
                i++;
            }

            // Apply the transformation
            ApplyToBuilder(transformation);
        }

        private bool CheckIsChange(TransformationResult transformation)
        {
            // Transformations with counts other than 1 always result in a change.
            if (transformation.Count != 1)
            { return true; }

            // Advance the enumerator to check what's in the original collection at the "current" location
            if (!Enumerator.MoveNext())
            {
                // The enumerator has no more elements, so this is an addition.
                return true;
            }

            // Check if the translations are the same
            TranslatedDeclaration oldDeclaration = Enumerator.Current;
            TranslatedDeclaration newDeclaration = transformation.SingleDeclaration;

            if (ReferenceEquals(oldDeclaration, newDeclaration))
            {
                // The references are equal, no change needed
                // Advanced LastGoodIndex to indicate that the elements up to this index are good
                LastGoodIndex++;
                return false;
            }
            else
            {
                // Equality check failed, the collection is being changed.
                return true;
            }
        }

        /// <summary>Appends the given transformation result to this collection.</summary>
        public void Add(TransformationResult transformation)
        {
            if (IsFinished)
            { throw new InvalidOperationException("Can't add to a collection once it's been finished."); }

            // If we were already changed, just add the results
            if (WasChanged)
            {
                ApplyToBuilder(transformation);
                return;
            }

            // If this transformation doesn't change the collection, do nothing.
            if (!CheckIsChange(transformation))
            { return; }

            // If we got this far, create a builder and apply our change
            CreateBuilder(transformation);
        }

        /// <summary>Indicates that the collection is complete and no more calls to <see cref="Add(TransformationResult)"/> will be performed.</summary>
        /// <remarks>
        /// Calling this method will change the value of <see cref="WasChanged"/> in the event the collection was truncated.
        ///
        /// After marking a collection as finished, you can no longer call <see cref="Add(TransformationResult)"/>.
        /// </remarks>
        public void Finish()
        {
            // If we've already been finished, there's nothing to do.
            if (IsFinished)
            { return; }

            // Mark this instance as finished to prevent further adds
            IsFinished = true;

            // If we were changed there is nothing to do
            if (WasChanged)
            { return; }

            // Advance the enumerator, if it ran out of elements we exhausted the entire original list
            if (!Enumerator.MoveNext())
            { return; }

            // If we *didn't* exhaust the entire original list, the list was truncated so we'll need to create a new one.
            // (A default TransformationResult normally results in a deletion, but in this case we use it to add nothing.)
            // (We explicitly size to the last good index to avoid resizing the builder later.)
            CreateBuilder(default, LastGoodIndex + 1);
        }

        /// <summary>Constructs a new <see cref="ImmutableArray{TDeclaration}"/> based on the changes made to this instance.</summary>
        /// <returns>A modified collection if changes were made, the original collection otherwise.</returns>
        /// <remarks>
        /// This method automatically calls <see cref="Finish"/>. As such, you can no longer add to this instance after calling this method.
        ///
        /// Additionally, this method cannot be called more than once.
        /// </remarks>
        public ImmutableArray<TDeclaration> MoveToImmutable()
        {
            if (MoveToImmutableCalled)
            { throw new InvalidOperationException("This method can only be called once."); }

            MoveToImmutableCalled = true;

            Finish();

            if (Builder is not null)
            {
                Builder.Capacity = Builder.Count;
                return Builder.MoveToImmutable();
            }
            else
            { return Original; }
        }

        /// <summary>Constructs a collection of declarations added to this instance that weren't of the expected type.</summary>
        /// <returns>If <see cref="HasOtherDeclarations"/> is true, returns a collection of declarations. Otherwise an empty collection is returned.</returns>
        /// <remarks>
        /// This method must not be called more than once.
        /// 
        /// Unlike <see cref="MoveToImmutable"/>, this will not call <see cref="Finish"/>.
        /// </remarks>
        public ImmutableArray<TranslatedDeclaration> GetOtherDeclarations()
        {
            if (GetOtherDeclarationsCalled)
            { throw new InvalidOperationException("This method can only be called once."); }

            GetOtherDeclarationsCalled = true;

            return OtherDeclarationsBuilder?.MoveToImmutable() ?? ImmutableArray<TranslatedDeclaration>.Empty;
        }
    }
}
