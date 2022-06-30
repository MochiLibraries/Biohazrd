using Biohazrd.Expressions;
using ClangSharp;
using System.Diagnostics;

namespace Biohazrd;

public sealed record TranslatedTemplateConstantParameter : TranslatedTemplateParameter
{
    public TypeReference Type { get; init; }
    public ConstantValue? DefaultValue { get; init; }

    internal TranslatedTemplateConstantParameter(TranslatedFile file, NonTypeTemplateParmDecl parameter)
         : base(file, parameter)
    {
        Type = new ClangTypeReference(parameter.Type);
        IsParameterPack = parameter.IsParameterPack;

        if (parameter.HasDefaultArgument)
        {
            DefaultValue = parameter.DefaultArgument.TryComputeConstantValue(out TranslationDiagnostic? diagnostic);
            Diagnostics = Diagnostics.AddIfNotNull(diagnostic);
            Debug.Assert(DefaultValue is not null || diagnostic is not null, "Non-type template parameters are expected to have a constant default value.");
        }
    }

    public override string ToString()
        => $"{Type} {base.ToString()}";
}
