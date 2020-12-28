namespace Biohazrd.CSharp.Infrastructure
{
    public interface ICustomCSharpTranslatedDeclaration
    {
        void GenerateOutput(ICSharpOutputGenerator outputGenerator, VisitorContext context, CSharpCodeWriter writer);

        /// <summary>Determines if this custom declaration has output or not.</summary>
        /// <remarks>If overridden to return <c>false</c>, no file will be created for this declaration when it is at the root of the library and <see cref="LibraryTranslationMode.OneFilePerType"/> is used.</remarks>
        public bool HasOutput => true;
    }
}
