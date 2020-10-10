namespace Biohazrd.Transformation.Common
{
    public sealed class RemoveExplicitBitFieldPaddingFieldsTransformation : TransformationBase
    {
        protected override TransformationResult TransformBitField(TransformationContext context, TranslatedBitField declaration)
        {
            if (declaration.IsUnnamed || declaration.BitWidth == 0)
            { return null; }
            else
            { return declaration; }
        }
    }
}
