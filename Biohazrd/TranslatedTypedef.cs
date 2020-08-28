using ClangSharp;

namespace Biohazrd
{
    public sealed record TranslatedTypedef : TranslatedDeclaration
    {
        public TranslatedTypeReference UnderlyingType { get; }

        internal TranslatedTypedef(TranslatedFile file, TypedefDecl typedef)
            : base(file, typedef)
        {
            UnderlyingType = new TranslatedTypeReference(typedef.UnderlyingType);
        }
    }
}
