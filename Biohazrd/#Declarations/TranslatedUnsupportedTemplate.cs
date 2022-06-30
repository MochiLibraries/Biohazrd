using ClangSharp;

namespace Biohazrd;

public sealed record TranslatedUnsupportedTemplate : TranslatedTemplate
{
    //TODO: Should this always get a diagnostic?
    public TranslatedUnsupportedTemplate(TranslatedFile file, TemplateDecl template)
        : base(file, template)
    { }

    public override string ToString()
        => $"Unsupported{base.ToString()}";
}
