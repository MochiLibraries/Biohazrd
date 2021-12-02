namespace Biohazrd
{
    partial class DeclarationVisitor
    {
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

        protected virtual void VisitConstant(VisitorContext context, TranslatedConstant declaration)
            => VisitDeclaration(context, declaration);
        protected virtual void VisitEnum(VisitorContext context, TranslatedEnum declaration)
            => VisitDeclaration(context, declaration);
        protected virtual void VisitEnumConstant(VisitorContext context, TranslatedEnumConstant declaration)
            => VisitDeclaration(context, declaration);
        protected virtual void VisitFunction(VisitorContext context, TranslatedFunction declaration)
            => VisitDeclaration(context, declaration);
        protected virtual void VisitParameter(VisitorContext context, TranslatedParameter declaration)
            => VisitDeclaration(context, declaration);
        protected virtual void VisitRecord(VisitorContext context, TranslatedRecord declaration)
            => VisitDeclaration(context, declaration);
        protected virtual void VisitStaticField(VisitorContext context, TranslatedStaticField declaration)
            => VisitDeclaration(context, declaration);
        protected virtual void VisitTypedef(VisitorContext context, TranslatedTypedef declaration)
            => VisitDeclaration(context, declaration);
        protected virtual void VisitUndefinedRecord(VisitorContext context, TranslatedUndefinedRecord declaration)
            => VisitDeclaration(context, declaration);
        protected virtual void VisitUnsupportedDeclaration(VisitorContext context, TranslatedUnsupportedDeclaration declaration)
            => VisitDeclaration(context, declaration);
        protected virtual void VisitVTable(VisitorContext context, TranslatedVTable declaration)
            => VisitDeclaration(context, declaration);
        protected virtual void VisitVTableEntry(VisitorContext context, TranslatedVTableEntry declaration)
            => VisitDeclaration(context, declaration);

        protected virtual void VisitField(VisitorContext context, TranslatedField declaration)
            => VisitDeclaration(context, declaration);
        protected virtual void VisitBaseField(VisitorContext context, TranslatedBaseField declaration)
            => VisitField(context, declaration);
        protected virtual void VisitNormalField(VisitorContext context, TranslatedNormalField declaration)
            => VisitField(context, declaration);
        protected virtual void VisitBitField(VisitorContext context, TranslatedBitField declaration)
            => VisitNormalField(context, declaration);
        protected virtual void VisitUnimplementedField(VisitorContext context, TranslatedUnimplementedField declaration)
            => VisitField(context, declaration);
        protected virtual void VisitVTableField(VisitorContext context, TranslatedVTableField declaration)
            => VisitField(context, declaration);
    }
}
