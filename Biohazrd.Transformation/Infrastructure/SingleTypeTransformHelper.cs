using System;

namespace Biohazrd.Transformation.Infrastructure
{
    /// <summary>
    /// Provides infrastructure for consistent transforming of a <see cref="TypeReference"/> backed by a single-value storage (IE: a field or property)) for <see cref="TypeTransformationBase"/>.
    /// </summary>
    /// <remarks>Diagnostics emitted by the added transformations are accumulated in the assocaited <see cref="DiagnosticAccumulator"/>.</remarks>
    public ref struct SingleTypeTransformHelper
    {
        private DiagnosticAccumulatorRef Diagnostics;
        private readonly TypeReference Original;
        /// <summary>The transformed value for the field.</summary>
        /// <remarks>Will return the original value before <see cref="SetValue(TypeTransformationResult)"/> is called.</remarks>
        public TypeReference NewValue { get; private set; }
        private bool IsSet; // Note: It is OK to access NewValue when this is false because we might only get set conditionally.

        public bool WasChanged => NewValue != Original;

        public SingleTypeTransformHelper(TypeReference original, ref DiagnosticAccumulator diagnosticAccumulator)
        {
            Diagnostics = new DiagnosticAccumulatorRef(ref diagnosticAccumulator);
            Original = original;
            NewValue = original;
            IsSet = false;
        }

        /// <summary>Set ths value of this instance to the given transformation result.</summary>
        public void SetValue(TypeTransformationResult result)
        {
            if (IsSet)
            { throw new InvalidOperationException("Cannot set the value more than once."); }

            IsSet = true;

            NewValue = result.TypeReference;

            if (result.Diagnostics.Length > 0)
            { Diagnostics.AddRange(result.Diagnostics); }
        }
    }
}
