namespace Biohazrd.Transformation.Infrastructure
{
    public interface ICustomTranslatedDeclaration
    {
        TransformationResult TransformChildren(ITransformation transformation, TransformationContext context);
        TransformationResult TransformTypeChildren(ITypeTransformation transformation, TransformationContext context);
    }
}
