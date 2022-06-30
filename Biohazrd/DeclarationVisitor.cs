namespace Biohazrd
{
    public abstract partial class DeclarationVisitor
    {
        public void Visit(TranslatedLibrary library)
        {
            VisitorContext context = new(library);

            // Visit each child of the library
            foreach (TranslatedDeclaration declaration in library.Declarations)
            { Visit(context, declaration); }
        }

        protected virtual void VisitDeclaration(VisitorContext context, TranslatedDeclaration declaration)
        {
            // Visit children
            bool first = true;
            foreach (TranslatedDeclaration child in declaration)
            {
                if (first)
                {
                    first = false;
                    context = context.Add(declaration);
                }

                Visit(context, child);
            }
        }

        protected virtual void VisitUnknownDeclarationType(VisitorContext context, TranslatedDeclaration declaration)
            => VisitDeclaration(context, declaration);
    }
}
