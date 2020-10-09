namespace Biohazrd.CSharp.Infrastructure
{
    public interface ICustomCSharpTypeReference
    {
        string GetTypeAsString(ICSharpOutputGenerator outputTranslator, VisitorContext context, TranslatedDeclaration declaration);
    }
}
