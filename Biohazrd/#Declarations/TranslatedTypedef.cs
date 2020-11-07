using ClangSharp;

namespace Biohazrd
{
    public sealed record TranslatedTypedef : TranslatedDeclaration
    {
        public TypeReference UnderlyingType { get; init; }

        internal TranslatedTypedef(TranslatedFile file, TypedefDecl typedef)
            : base(file, typedef)
            => UnderlyingType = new ClangTypeReference(typedef.UnderlyingType);

        internal TranslatedTypedef(TranslatedFile file, TypeAliasDecl alias)
            : base(file, alias)
            => UnderlyingType = new ClangTypeReference(alias.UnderlyingType);

        public override string ToString()
            => $"Typedef {base.ToString()} -> {UnderlyingType}";
    }
}
