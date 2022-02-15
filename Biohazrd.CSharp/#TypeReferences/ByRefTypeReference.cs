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
    {
        string keyword;

        //TODO: This is slightly brittle since we don't actually know what the emit context is (only the declaration context.)
        if (declaration is TranslatedParameter)
        { keyword = Kind.GetKeywordForParameter(); }
        else
        { keyword = Kind.GetKeywordForReturn(); }

        return $"{keyword} {outputGenerator.GetTypeAsString(context, declaration, Inner)}";
    }

    public override string ToString()
        => $"{Kind.GetKeywordForParameter()} {Inner}";
}
