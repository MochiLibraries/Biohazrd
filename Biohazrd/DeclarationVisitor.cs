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

        protected virtual void Visit(VisitorContext context, TranslatedDeclaration declaration)
        {
            switch (declaration)
            {
                // Fields
                case TranslatedVTableField vTableFieldDeclaration:
                    VisitVTableField(context, vTableFieldDeclaration);
                    break;
                case TranslatedUnimplementedField unimplementedFieldDeclaration:
                    VisitUnimplementedField(context, unimplementedFieldDeclaration);
                    break;
                case TranslatedNormalField normalFieldDeclaration:
                    VisitNormalField(context, normalFieldDeclaration);
                    break;
                case TranslatedBaseField baseFieldDeclaration:
                    VisitBaseField(context, baseFieldDeclaration);
                    break;
                case TranslatedField fieldDeclaration:
                    VisitField(context, fieldDeclaration);
                    break;

                // Sealed children of TranslatedDeclaration
                case TranslatedVTableEntry vTableEntry:
                    VisitVTableEntry(context, vTableEntry);
                    break;
                case TranslatedVTable vTableDeclaration:
                    VisitVTable(context, vTableDeclaration);
                    break;
                case TranslatedUnsupportedDeclaration unsupportedDeclarationDeclaration:
                    VisitUnsupportedDeclaration(context, unsupportedDeclarationDeclaration);
                    break;
                case TranslatedUndefinedRecord undefinedRecordDeclaration:
                    VisitUndefinedRecord(context, undefinedRecordDeclaration);
                    break;
                case TranslatedTypedef typedefDeclaration:
                    VisitTypedef(context, typedefDeclaration);
                    break;
                case TranslatedStaticField staticFieldDeclaration:
                    VisitStaticField(context, staticFieldDeclaration);
                    break;
                case TranslatedRecord recordDeclaration:
                    VisitRecord(context, recordDeclaration);
                    break;
                case TranslatedParameter parameterDeclaration:
                    VisitParameter(context, parameterDeclaration);
                    break;
                case TranslatedFunction functionDeclaration:
                    VisitFunction(context, functionDeclaration);
                    break;
                case TranslatedEnumConstant enumConstantDeclaration:
                    VisitEnumConstant(context, enumConstantDeclaration);
                    break;
                case TranslatedEnum enumDeclaration:
                    VisitEnum(context, enumDeclaration);
                    break;

                // Fallback declaration
                default:
                    VisitUnknownDeclarationType(context, declaration);
                    break;
            }
        }

        protected virtual void VisitUnknownDeclarationType(VisitorContext context, TranslatedDeclaration declaration)
            => VisitDeclaration(context, declaration);
    }
}
