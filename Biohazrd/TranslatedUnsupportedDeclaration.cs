using ClangSharp;

namespace Biohazrd
{
    public sealed record TranslatedUnsupportedDeclaration : TranslatedDeclaration
    {
        internal TranslatedUnsupportedDeclaration(TranslatedFile file, Decl declaration, Severity severity, string reason)
            : base(file, declaration)
            => Diagnostics = Diagnostics.Add(severity, declaration, reason);
    }
}
