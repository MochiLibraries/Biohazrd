using ClangSharp;

namespace Biohazrd;

public abstract record TranslatedTemplateParameter : TranslatedDeclaration
{
    /// <summary>True if the template parameter is a parameter pack (IE: the parameter is vardic.)</summary>
    public bool IsParameterPack { get; init; }

    protected TranslatedTemplateParameter(TranslatedFile file, NamedDecl namedDecl)
        : base(file, namedDecl)
    { }

    internal static TranslatedTemplateParameter Create(TranslatedFile file, NamedDecl decl)
        => decl switch
        {
            TemplateTypeParmDecl typeParameter => new TranslatedTemplateTypeParameter(file, typeParameter),
            NonTypeTemplateParmDecl nonTypeParameter => new TranslatedTemplateConstantParameter(file, nonTypeParameter),
            _ => new TranslatedUnsupportedTemplateParameter(file, decl)
        };

    public override string ToString()
        => base.ToString();
}
