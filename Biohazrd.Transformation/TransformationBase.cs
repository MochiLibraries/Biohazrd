namespace Biohazrd.Transformation
{
    public abstract class TransformationBase : RawTransformationBase
    {
        protected override TransformationResult Transform(TransformationContext context, TranslatedDeclaration declaration)
            => declaration switch
            {
                // Fields
                TranslatedVTableField vTableFieldDeclaration => TransformVTableField(context, vTableFieldDeclaration),
                TranslatedUnimplementedField unimplementedFieldDeclaration => TransformUnimplementedField(context, unimplementedFieldDeclaration),
                TranslatedNormalField normalFieldDeclaration => TransformNormalField(context, normalFieldDeclaration),
                TranslatedBaseField baseFieldDeclaration => TransformBaseField(context, baseFieldDeclaration),
                TranslatedField fieldDeclaration => TransformField(context, fieldDeclaration),
                // Sealed children of TranslatedDeclaration
                TranslatedVTableEntry vTableEntry => TransformVTableEntry(context, vTableEntry),
                TranslatedVTable vTableDeclaration => TransformVTable(context, vTableDeclaration),
                TranslatedUnsupportedDeclaration unsupportedDeclarationDeclaration => TransformUnsupportedDeclaration(context, unsupportedDeclarationDeclaration),
                TranslatedUndefinedRecord undefinedRecordDeclaration => TransformUndefinedRecord(context, undefinedRecordDeclaration),
                TranslatedTypedef typedefDeclaration => TransformTypedef(context, typedefDeclaration),
                TranslatedStaticField staticFieldDeclaration => TransformStaticField(context, staticFieldDeclaration),
                TranslatedRecord recordDeclaration => TransformRecord(context, recordDeclaration),
                TranslatedParameter parameterDeclaration => TransformParameter(context, parameterDeclaration),
                TranslatedFunction functionDeclaration => TransformFunction(context, functionDeclaration),
                TranslatedEnumConstant enumConstantDeclaration => TransformEnumConstant(context, enumConstantDeclaration),
                TranslatedEnum enumDeclaration => TransformEnum(context, enumDeclaration),
                // Fallback declaration
                TranslatedDeclaration => TransformUnknownDeclarationType(context, declaration)
            };

        protected virtual TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
            => declaration;

        protected virtual TransformationResult TransformUnknownDeclarationType(TransformationContext context, TranslatedDeclaration declaration)
            => TransformDeclaration(context, declaration);

        protected virtual TransformationResult TransformEnum(TransformationContext context, TranslatedEnum declaration)
            => TransformDeclaration(context, declaration);
        protected virtual TransformationResult TransformEnumConstant(TransformationContext context, TranslatedEnumConstant declaration)
            => TransformDeclaration(context, declaration);
        protected virtual TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
            => TransformDeclaration(context, declaration);
        protected virtual TransformationResult TransformParameter(TransformationContext context, TranslatedParameter declaration)
            => TransformDeclaration(context, declaration);
        protected virtual TransformationResult TransformRecord(TransformationContext context, TranslatedRecord declaration)
            => TransformDeclaration(context, declaration);
        protected virtual TransformationResult TransformStaticField(TransformationContext context, TranslatedStaticField declaration)
            => TransformDeclaration(context, declaration);
        protected virtual TransformationResult TransformTypedef(TransformationContext context, TranslatedTypedef declaration)
            => TransformDeclaration(context, declaration);
        protected virtual TransformationResult TransformUndefinedRecord(TransformationContext context, TranslatedUndefinedRecord declaration)
            => TransformDeclaration(context, declaration);
        protected virtual TransformationResult TransformUnsupportedDeclaration(TransformationContext context, TranslatedUnsupportedDeclaration declaration)
            => TransformDeclaration(context, declaration);
        protected virtual TransformationResult TransformVTable(TransformationContext context, TranslatedVTable declaration)
            => TransformDeclaration(context, declaration);
        protected virtual TransformationResult TransformVTableEntry(TransformationContext context, TranslatedVTableEntry declaration)
            => TransformDeclaration(context, declaration);

        protected virtual TransformationResult TransformField(TransformationContext context, TranslatedField declaration)
            => TransformDeclaration(context, declaration);
        protected virtual TransformationResult TransformBaseField(TransformationContext context, TranslatedBaseField declaration)
            => TransformField(context, declaration);
        protected virtual TransformationResult TransformNormalField(TransformationContext context, TranslatedNormalField declaration)
            => TransformField(context, declaration);
        protected virtual TransformationResult TransformUnimplementedField(TransformationContext context, TranslatedUnimplementedField declaration)
            => TransformField(context, declaration);
        protected virtual TransformationResult TransformVTableField(TransformationContext context, TranslatedVTableField declaration)
            => TransformField(context, declaration);
    }
}
