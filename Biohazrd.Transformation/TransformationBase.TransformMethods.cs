namespace Biohazrd.Transformation
{
    partial class TransformationBase
    {
        protected virtual TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
            => declaration;

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
