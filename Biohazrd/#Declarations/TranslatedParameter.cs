using Biohazrd.Expressions;
using ClangSharp;

namespace Biohazrd
{
    public sealed record TranslatedParameter : TranslatedDeclaration
    {
        public TypeReference Type { get; init; }
        public bool ImplicitlyPassedByReference { get; init; }

        public ConstantValue? DefaultValue { get; init; }

        internal unsafe TranslatedParameter(TranslatedFile file, ParmVarDecl parameter)
            : base(file, parameter)
        {
            Type = new ClangTypeReference(parameter.Type);
            ImplicitlyPassedByReference = parameter.Type.MustBePassedByReference(isForInstanceMethodReturnValue: false);

            DefaultValue = parameter.TryComputeConstantValue(out TranslationDiagnostic? diagnostic);
            Diagnostics = Diagnostics.AddIfNotNull(diagnostic);

            if (DefaultValue is null && diagnostic is null && parameter.HasInit)
            { Diagnostics = Diagnostics.Add(Severity.Warning, "Non-constant default parameter values are not supported."); }
        }

        public override string ToString()
            => $"Parameter {base.ToString()}";
    }
}
