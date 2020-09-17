using System;
using System.Collections.Immutable;

namespace Biohazrd.Transformation.Infrastructure
{
    /// <summary>Provides infrastructure for consistent transforming of a <see cref="TranslatedDeclaration"/> backed by a single-value storage (IE: a field or property)) for <see cref="TransformationBase"/>.</summary>
    /// <remarks>
    /// When the transformation of a single <see cref="TranslatedDeclaration"/> changes type or results in extra declarations, this type aids in enumerating the extra or unrealted types.
    ///
    /// Consumers of this type are expected to use <see cref="HasExtraValues"/> and <see cref="ExtraValues"/> to handle the case where
    /// a transformation yields multiple declarations or a declaration of an unexpected type.
    /// </remarks>
    public ref struct SingleTransformHelper<TDeclaration>
        where TDeclaration : TranslatedDeclaration
    {
        private readonly TDeclaration? Original;
        /// <summary>The transformed value for the field.</summary>
        /// <remarks>May be <c>null</c> if the transformation deleted the original declaration or replaced it with a different type of declaration.</remarks>
        public TDeclaration? NewValue { get; private set; }
        private ImmutableList<TranslatedDeclaration>.Builder? ExtraValuesBuilder;
        /// <summary>Retrieves the extra values returns by the transformation.</summary>
        /// <remarks>This may contain values when <see cref="NewValue"/> is <c>null</c> in the event that all of the results from the transformation were not of <typeparamref name="TDeclaration"/>.</remarks>
        public ImmutableList<TranslatedDeclaration> ExtraValues => ExtraValuesBuilder?.ToImmutable() ?? ImmutableList<TranslatedDeclaration>.Empty;
        private bool IsSet; // Note: It should not be considered invalid to get the value of NewValue when this is false because we might only be set conditionally.

        /// <summary>True if the set value is a change from the original value of the field.</summary>
        public bool WasChanged => ExtraValuesBuilder is not null || !ReferenceEquals(Original, NewValue);

        /// <summary>True if <see cref="ExtraValues"/> contains any values.</summary>
        /// <remarks>This is always false when <see cref="WasChanged"/> is false.</remarks>
        public bool HasExtraValues => ExtraValuesBuilder?.Count > 0;

        public SingleTransformHelper(TDeclaration? original)
        {
            Original = original;
            NewValue = null;
            ExtraValuesBuilder = null;
            IsSet = false;
        }

        private void SetValue(TranslatedDeclaration declaration)
        {
            if (declaration is TDeclaration tDeclaration && NewValue is null)
            {
                NewValue = tDeclaration;
                return;
            }

            if (ExtraValuesBuilder is null)
            { ExtraValuesBuilder = ImmutableList.CreateBuilder<TranslatedDeclaration>(); }

            ExtraValuesBuilder.Add(declaration);
        }

        /// <summary>Set ths value of this instance to the given transformation result.</summary>
        /// <remarks>
        /// When <paramref name="result"/> refers to more than one declaration, only the first result of type <typeparamref name="TDeclaration"/> will be stored in <see cref="NewValue"/>.
        /// All others will be placed in <see cref="ExtraValues"/>.
        /// </remarks>
        public void SetValue(TransformationResult result)
        {
            if (IsSet)
            { throw new InvalidOperationException("Cannot set the value more than once."); }

            IsSet = true;

            switch (result.Count)
            {
                case 1:
                    SetValue(result.SingleDeclaration);
                    return;
                case > 1:
                    foreach (TranslatedDeclaration declaration in result.Declarations)
                    { SetValue(declaration); }
                    return;
            }
        }
    }
}
