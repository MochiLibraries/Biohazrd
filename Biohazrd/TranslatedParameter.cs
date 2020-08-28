using ClangSharp;

namespace Biohazrd
{
    public sealed record TranslatedParameter : TranslatedDeclaration
    {
        public TranslatedTypeReference Type { get; init; }

        public TranslatedParameter(TranslatedFile file, ParmVarDecl parameter)
            : base(file, parameter)
            => Type = new TranslatedTypeReference(parameter.Type);
    }
}
