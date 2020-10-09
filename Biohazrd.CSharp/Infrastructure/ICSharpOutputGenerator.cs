namespace Biohazrd.CSharp.Infrastructure
{
    public interface ICSharpOutputGenerator
    {
        string GetTypeAsString(VisitorContext context, TranslatedDeclaration declaration, TypeReference type);

        void AddUsing(string @namespace);
    }
}
