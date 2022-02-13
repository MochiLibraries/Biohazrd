namespace Biohazrd.CSharp.Metadata;

/// <summary>The presence of this metadata on a parameter overrides <see cref="CSharpGenerationOptions."/></summary>
public struct OverrideReferenceTypeOutputBehavior : IDeclarationMetadataItem
{
    public ReferenceTypeOutputBehavior ReferenceTypeOutputBehavior { get; }

    public OverrideReferenceTypeOutputBehavior(ReferenceTypeOutputBehavior referenceTypeOutputBehavior)
        => ReferenceTypeOutputBehavior = referenceTypeOutputBehavior;
}
