using ClangSharp;

namespace Biohazrd
{
    public sealed record TranslatedUnsupportedDeclaration : TranslatedDeclaration
    {
        internal TranslatedUnsupportedDeclaration(TranslatedFile file, Decl declaration, Severity severity, string reason)
            : base(file, declaration)
            => Diagnostics = Diagnostics.Add(severity, declaration, reason);

        public TranslatedUnsupportedDeclaration(Decl declaration, Severity severity, string reason)
            : this(TranslatedFile.Synthesized, declaration, severity, reason)
        { }

        public TranslatedUnsupportedDeclaration(Severity severity, string reason)
            : base(TranslatedFile.Synthesized)
            => Diagnostics = Diagnostics.Add(severity, reason);

        public override string ToString()
            => $"Unsupported Declaration {base.ToString()}";
    }
}
