namespace Biohazrd.Transformation.Infrastructure
{
    public interface ITypeTransformation
    {
        TypeTransformationResult TransformTypeRecursively(TypeTransformationContext context, TypeReference type);
    }
}
