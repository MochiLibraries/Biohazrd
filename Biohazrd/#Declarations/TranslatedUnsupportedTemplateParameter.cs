using ClangSharp;

namespace Biohazrd;

public sealed record TranslatedUnsupportedTemplateParameter : TranslatedTemplateParameter
{
    //TODO: Should this always get a diagnostic?
    internal TranslatedUnsupportedTemplateParameter(TranslatedFile file, NamedDecl decl)
        : base(file, decl)
    {
        IsParameterPack = decl is TemplateTemplateParmDecl { IsParameterPack: true };
    }

    public override string ToString()
        => $"Unsupported{Declaration?.GetType()?.Name ?? ""} {base.ToString()}";
}
