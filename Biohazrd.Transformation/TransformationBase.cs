namespace Biohazrd.Transformation
{
    public abstract partial class TransformationBase : RawTransformationBase
    {
        protected virtual TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
            => declaration;

        protected virtual TransformationResult TransformUnknownDeclarationType(TransformationContext context, TranslatedDeclaration declaration)
            => TransformDeclaration(context, declaration);
    }
}
