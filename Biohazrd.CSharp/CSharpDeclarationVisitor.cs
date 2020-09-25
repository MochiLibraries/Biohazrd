namespace Biohazrd.CSharp
{
    public abstract class CSharpDeclarationVisitor : DeclarationVisitor
    {
        protected override void Visit(VisitorContext context, TranslatedDeclaration declaration)
        {
            switch (declaration)
            {
                case SynthesizedLooseDeclarationsType synthesizedLooseDeclarationsType:
                    VisitSynthesizedLooseDeclarationsType(context, synthesizedLooseDeclarationsType);
                    return;
                default:
                    base.Visit(context, declaration);
                    return;
            }
        }

        protected virtual void VisitSynthesizedLooseDeclarationsType(VisitorContext context, SynthesizedLooseDeclarationsType declaration)
            => VisitDeclaration(context, declaration);
    }
}
