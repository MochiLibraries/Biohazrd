using ClangSharp;
using System.Collections.Immutable;

namespace Biohazrd;

/// <summary>Represents a C++ template which has not been instantiated</summary>
public abstract partial record TranslatedTemplate : TranslatedDeclaration
{
    public ImmutableArray<TranslatedTemplateParameter> Parameters { get; init; }

    public TranslatedTemplate(TranslatedFile file, TemplateDecl template)
        : base(file, template)
    {
        ImmutableArray<TranslatedTemplateParameter>.Builder parametersBuilder = ImmutableArray.CreateBuilder<TranslatedTemplateParameter>(template.TemplateParameters.Count);

        foreach (NamedDecl parameter in template.TemplateParameters)
        { parametersBuilder.Add(TranslatedTemplateParameter.Create(file, parameter)); }

        Parameters = parametersBuilder.MoveToImmutable();
    }

    public override string ToString()
        => $"Template {base.ToString()}";
}
