using Biohazrd.Expressions;

namespace Biohazrd.CSharp.Infrastructure
{
    public interface ICSharpOutputGenerator
    {
        CSharpGenerationOptions Options { get; }

        string GetTypeAsString(VisitorContext context, TranslatedDeclaration declaration, TypeReference type);

        string GetConstantAsString(VisitorContext context, TranslatedDeclaration declaration, ConstantValue constant, TypeReference targetType);

        void AddUsing(string @namespace);

        void Visit(VisitorContext context, TranslatedDeclaration declaration);

        void AddDiagnostic(TranslationDiagnostic diagnostic);

        void AddDiagnostic(Severity severity, string message)
            => AddDiagnostic(new TranslationDiagnostic(severity, message));
    }
}
