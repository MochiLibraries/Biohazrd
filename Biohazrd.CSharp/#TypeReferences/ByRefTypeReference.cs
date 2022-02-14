using Biohazrd.CSharp.Infrastructure;
using Biohazrd.Transformation;
using Biohazrd.Transformation.Infrastructure;
using System;

namespace Biohazrd.CSharp;

public sealed record ByRefTypeReference : TypeReference, ICustomTypeReference, ICustomCSharpTypeReference
{
    private ByRefKind _Kind;
    public ByRefKind Kind
    {
        get => _Kind;
        init
        {
            if (!Enum.IsDefined(value))
            { throw new ArgumentOutOfRangeException(nameof(value)); }

            _Kind = value;
        }
    }

    public TypeReference Inner { get; init; }

    public string Keyword => Kind.GetKeyword();

    public ByRefTypeReference(ByRefKind kind, TypeReference inner)
    {
        if (!Enum.IsDefined(kind))
        { throw new ArgumentOutOfRangeException(nameof(kind)); }

        _Kind = kind;
        Inner = inner;
    }

    TypeTransformationResult ICustomTypeReference.TransformChildren(ITypeTransformation transformation, TypeTransformationContext context)
    {
        DiagnosticAccumulator diagnostics = new();
        SingleTypeTransformHelper newInnerType = new(Inner, ref diagnostics);

        // Transform inner type
        newInnerType.SetValue(transformation.TransformTypeRecursively(context, Inner));

        // Create the result
        TypeTransformationResult result = newInnerType.WasChanged ? this with { Inner = newInnerType.NewValue } : this;
        result.AddDiagnostics(diagnostics.MoveToImmutable());
        return result;
    }

    string ICustomCSharpTypeReference.GetTypeAsString(ICSharpOutputGenerator outputGenerator, VisitorContext context, TranslatedDeclaration declaration)
        => $"{Keyword} {outputGenerator.GetTypeAsString(context, declaration, Inner)}";

    public override string ToString()
        => $"{Keyword} {Inner}";
}
