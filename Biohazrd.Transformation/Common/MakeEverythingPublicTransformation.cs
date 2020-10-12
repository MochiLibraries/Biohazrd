namespace Biohazrd.Transformation.Common
{
    public sealed class MakeEverythingPublicTransformation : TransformationBase
    {
        protected override TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
        {
            if (declaration.Accessibility != AccessModifier.Public)
            {
                return declaration with
                {
                    Accessibility = AccessModifier.Public
                };
            }

            return declaration;
        }
    }
}
