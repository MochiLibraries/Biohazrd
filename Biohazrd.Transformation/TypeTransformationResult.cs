using System.Collections.Immutable;
using System.Text;

namespace Biohazrd.Transformation
{
    public ref struct TypeTransformationResult
    {
        public TypeReference TypeReference { get; init; }
        public ImmutableArray<TranslationDiagnostic> Diagnostics { get; init; }

        public bool IsChange(TypeReference originalTypereference)
            => Diagnostics.Length > 0 || TypeReference != originalTypereference;

        public TypeTransformationResult(TypeReference typeReference)
        {
            TypeReference = typeReference;
            Diagnostics = ImmutableArray<TranslationDiagnostic>.Empty;
        }

        public TypeTransformationResult(TypeReference typeReference, TranslationDiagnostic diagnostic)
        {
            TypeReference = typeReference;
            Diagnostics = ImmutableArray.Create(diagnostic);
        }

        public TypeTransformationResult(TypeReference typeReference, ImmutableArray<TranslationDiagnostic> diagnostics)
        {
            TypeReference = typeReference;
            Diagnostics = diagnostics;
        }

        public TypeTransformationResult(TypeReference typeReference, Severity diagnosticSeverity, string diagnosticMessage)
            : this(typeReference, new TranslationDiagnostic(diagnosticSeverity, diagnosticMessage))
        { }

        public TypeTransformationResult(TypeTransformationResult other)
            => this = other;

        public TypeTransformationResult AddDiagnostic(TranslationDiagnostic diagnostic)
            => new TypeTransformationResult(this)
            {
                Diagnostics = this.Diagnostics.Add(diagnostic)
            };

        public TypeTransformationResult AddDiagnostic(Severity severity, string diagnosticMessage)
            => AddDiagnostic(new TranslationDiagnostic(severity, diagnosticMessage));

        public TypeTransformationResult AddDiagnostics(ImmutableArray<TranslationDiagnostic> diagnostics)
            => new TypeTransformationResult(this)
            {
                Diagnostics = this.Diagnostics.AddRange(diagnostics)
            };

        public TypeTransformationResult WithType(TypeReference typeReference)
            => new TypeTransformationResult(this)
            {
                TypeReference = typeReference
            };

        public static implicit operator TypeTransformationResult(TypeReference typeReference)
            => new TypeTransformationResult(typeReference);

        public override string ToString()
        {
            if (Diagnostics.Length == 0)
            { return TypeReference.ToString(); }

            StringBuilder builder = new();
            builder.Append(TypeReference);

            if (Diagnostics.Length == 1)
            { builder.Append($" ({Diagnostics[0]})"); }
            else
            { builder.Append($" (With {Diagnostics.Length} diagnostics)"); }

            return builder.ToString();
        }
    }
}
