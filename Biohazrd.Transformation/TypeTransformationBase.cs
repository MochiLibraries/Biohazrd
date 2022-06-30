namespace Biohazrd.Transformation
{
    public abstract partial class TypeTransformationBase : RawTypeTransformationBase
    {
        protected virtual TypeTransformationResult TransformTypeReference(TypeTransformationContext context, TypeReference type)
            => type;

        protected virtual TypeTransformationResult TransformUnknownTypeReference(TypeTransformationContext context, TypeReference type)
            => TransformTypeReference(context, type);
    }
}
