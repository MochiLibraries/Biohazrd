using Biohazrd.Expressions;

namespace Biohazrd.CSharp.Infrastructure
{
    public interface ICSharpOutputGenerator
    {
        string GetTypeAsString(VisitorContext context, TranslatedDeclaration declaration, TypeReference type);

        string GetConstantAsString(VisitorContext context, TranslatedDeclaration declaration, ConstantValue constant, TypeReference targetType);

        void AddUsing(string @namespace);
    }
}
