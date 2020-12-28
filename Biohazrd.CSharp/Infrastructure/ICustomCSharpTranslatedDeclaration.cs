namespace Biohazrd.CSharp.Infrastructure
{
    public interface ICustomCSharpTranslatedDeclaration
    {
        void GenerateOutput(ICSharpOutputGenerator outputGenerator, VisitorContext context, CSharpCodeWriter writer);

        /// <summary>Determines if this custom declaration has output or not.</summary>
        /// <remarks>If overridden to return <c>false</c>, no file will be created for this declaration when it is at the root of the library and <see cref="LibraryTranslationMode.OneFilePerType"/> is used.</remarks>
        public bool HasOutput => true;

        /// <summary>Determines how type references to this dedclaration should be emitted.</summary>
        /// <returns>The string to write to the output file to reference this type, or <c>null</c> if the default logic should be used.</returns>
        /// <remarks>
        /// By implementing this method, you can override the default type reference logic within <see cref="CSharpLibraryGenerator"/>.
        ///
        /// This method effectively provides an analog for <see cref="ICustomCSharpTypeReference.GetTypeAsString(ICSharpOutputGenerator, VisitorContext, TranslatedDeclaration)"/> for
        /// custom declarations referenced via <see cref="TranslatedTypeReference"/>.
        /// </remarks>
        public string? GetReferenceTypeAsString(ICSharpOutputGenerator outputGenerator, VisitorContext context, TranslatedDeclaration declaration)
            => null;
    }
}
