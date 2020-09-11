using ClangSharp;

namespace Biohazrd
{
    public sealed record TranslatedParameter : TranslatedDeclaration
    {
        public TypeReference Type { get; init; }

        public TranslatedParameter(TranslatedFile file, ParmVarDecl parameter)
            : base(file, parameter)
            => Type = new ClangTypeReference(parameter.Type);
    }
}
