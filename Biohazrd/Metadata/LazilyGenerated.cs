namespace Biohazrd.Metadata
{
    /// <summary>A marker used to indicate that the associated declaration is lazily generated.</summary>
    /// <remarks>
    /// A declaration which is marked as lazily generated will be stripped from the output if if is not referenced elsewhere in the generated output.
    ///
    /// This metadata item is only currently supported on <see cref="TranslatedRecord"/> declarations. It is implicitly added to anonymous unions/structs.
    ///
    /// The actual funcionality of this metadata is handled by <see cref="Biohazrd.Transformation.Common.StripUnreferencedLazyDeclarationsTransformation"/>.
    /// </remarks>
    public struct LazilyGenerated : IDeclarationMetadataItem
    {
    }
}
