namespace Biohazrd.CSharp.Infrastructure
{
    public interface ICustomCSharpConstantValue
    {
        string GetConstantAsString(ICSharpOutputGenerator outputTranslator, VisitorContext context, TranslatedDeclaration declaration);
    }
}
