using System.Collections.Immutable;

namespace Biohazrd.Expressions
{
    public struct ConstantEvaluationResult
    {
        public string Expression { get; }
        public ConstantValue? Value { get; }
        public ImmutableArray<TranslationDiagnostic> Diagnostics { get; }

        internal ConstantEvaluationResult(string expression, ConstantValue? value, ImmutableArray<TranslationDiagnostic> diagnostics)
        {
            Expression = expression;
            Value = value;
            Diagnostics = diagnostics;
        }
    }
}
