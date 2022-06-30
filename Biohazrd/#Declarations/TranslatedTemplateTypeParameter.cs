using ClangSharp;

namespace Biohazrd;

public sealed record TranslatedTemplateTypeParameter : TranslatedTemplateParameter
{
    public TypeReference? DefaultType { get; init; }

    internal TranslatedTemplateTypeParameter(TranslatedFile file, TemplateTypeParmDecl parameter)
        : base(file, parameter)
    {
        IsParameterPack = parameter.IsParameterPack;

        if (parameter.HasDefaultArgument)
        { DefaultType = new ClangTypeReference(parameter.DefaultArgument); }
    }

    public override string ToString()
        => $"typename {base.ToString()}";
}
