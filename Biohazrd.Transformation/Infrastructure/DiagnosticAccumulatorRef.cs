using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Biohazrd.Transformation.Infrastructure
{
    /// <summary>Black magic reference to a <see cref="DiagnosticAccumulator"/>.</summary>
    /// <remarks>Ideally this would be eliminated if https://github.com/dotnet/csharplang/issues/1147 ever happens.</remarks>
    internal unsafe ref struct DiagnosticAccumulatorRef
    {
        private readonly void* _DiagnosticsPtr;
        private ImmutableArray<TranslationDiagnostic>.Builder Diagnostics
        {
            get
            {
                Debug.Assert(_DiagnosticsPtr != null, $"{nameof(DiagnosticAccumulatorRef)} is not defaultable!");
                ref ImmutableArray<TranslationDiagnostic>.Builder? diagnosticsRef = ref Unsafe.AsRef<ImmutableArray<TranslationDiagnostic>.Builder?>(_DiagnosticsPtr);

                if (diagnosticsRef == null)
                { diagnosticsRef = ImmutableArray.CreateBuilder<TranslationDiagnostic>(); }

                return diagnosticsRef;
            }
        }

        internal DiagnosticAccumulatorRef(ref DiagnosticAccumulator accumulator)
            => _DiagnosticsPtr = Unsafe.AsPointer(ref accumulator._Diagnostics);

        public void Add(TranslationDiagnostic diagnostic)
            => Diagnostics.Add(diagnostic);

        public void AddRange(ImmutableArray<TranslationDiagnostic> diagnostics)
        {
            if (diagnostics.Length > 0)
            { Diagnostics.AddRange(diagnostics); }
        }
    }
}
