using Biohazrd.Expressions;
using System;

namespace Biohazrd
{
    /// <summary>A translated literal constant.</summary>
    /// <remarks>
    /// Biohazrd does not currently emit declarations of this type. This declaration is currently provided primarily for use in transformations.
    /// </remarks>
    public sealed record TranslatedConstant : TranslatedDeclaration
    {
        /// <summary>The type of this constant, if <c>null</c> the type should be inferred from <see cref="Value"/>.</summary>
        public TypeReference? Type { get; init; }
        public ConstantValue Value { get; init; }

        public TranslatedConstant(TranslatedFile file, string name, ConstantValue value)
            : base(file)
        {
            Name = name;
            Type = null;
            Value = value;
            Accessibility = AccessModifier.Public;
        }

        public TranslatedConstant(TranslatedFile file, string name, ConstantEvaluationResult evaluationResult)
            : this(file, name, evaluationResult.Value ?? throw new ArgumentException("The specified evaluation result has no value", nameof(evaluationResult)))
            => Diagnostics = Diagnostics.AddRange(evaluationResult.Diagnostics);

        public TranslatedConstant(string name, ConstantValue value)
            : this(TranslatedFile.Synthesized, name, value)
        { }

        public TranslatedConstant(string name, ConstantEvaluationResult evaluationResult)
            : this(TranslatedFile.Synthesized, name, evaluationResult)
        { }

        public override string ToString()
            => $"Constant {base.ToString()} = {Value}";
    }
}
