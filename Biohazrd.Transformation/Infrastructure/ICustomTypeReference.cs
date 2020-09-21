namespace Biohazrd.Transformation.Infrastructure
{
    public interface ICustomTypeReference
    {
        TypeTransformationResult TransformChildren(ITypeTransformation transformation, TypeTransformationContext context);
    }
}
