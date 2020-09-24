using ClangSharp;

namespace Biohazrd
{
    public sealed record TranslatedParameter : TranslatedDeclaration
    {
        public TypeReference Type { get; init; }
        public bool ImplicitlyPassedByReference { get; init; }

        internal TranslatedParameter(TranslatedFile file, ParmVarDecl parameter)
            : base(file, parameter)
        {
            Type = new ClangTypeReference(parameter.Type);
            ImplicitlyPassedByReference = parameter.Type.MustBePassedByReference();
        }

        public override string ToString()
            => $"Parameter {base.ToString()}";
    }
}
