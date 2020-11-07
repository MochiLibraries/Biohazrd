namespace Biohazrd.CSharp.Infrastructure
{
    public interface ICustomCSharpTypeReference
    {
        string GetTypeAsString(ICSharpOutputGenerator outputGenerator, VisitorContext context, TranslatedDeclaration declaration);
    }
}
