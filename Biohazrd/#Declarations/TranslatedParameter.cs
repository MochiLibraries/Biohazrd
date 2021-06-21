using Biohazrd.Expressions;
using ClangSharp;
using ClangSharp.Pathogen;

namespace Biohazrd
{
    public sealed record TranslatedParameter : TranslatedDeclaration
    {
        public TypeReference Type { get; init; }
        public bool ImplicitlyPassedByReference => AbiInfo.Kind == PathogenArgumentKind.Indirect;

        public ConstantValue? DefaultValue { get; init; }

        public PathogenArgumentInfo AbiInfo { get; }

        internal unsafe TranslatedParameter(TranslatedFile file, ParmVarDecl parameter, PathogenArgumentInfo abiInfo)
            : base(file, parameter)
        {
            Type = new ClangTypeReference(parameter.Type);

            DefaultValue = parameter.TryComputeConstantValue(out TranslationDiagnostic? diagnostic);
            Diagnostics = Diagnostics.AddIfNotNull(diagnostic);

            if (DefaultValue is null && diagnostic is null && parameter.HasInit)
            { Diagnostics = Diagnostics.Add(Severity.Warning, "Non-constant default parameter values are not supported."); }

            AbiInfo = abiInfo;
        }

        public override string ToString()
            => $"Parameter {base.ToString()}";
    }
}
