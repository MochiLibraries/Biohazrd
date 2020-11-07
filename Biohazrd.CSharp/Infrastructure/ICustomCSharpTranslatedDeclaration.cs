namespace Biohazrd.CSharp.Infrastructure
{
    public interface ICustomCSharpTranslatedDeclaration
    {
        void GenerateOutput(ICSharpOutputGenerator outputGenerator, VisitorContext context, CSharpCodeWriter writer);
    }
}
