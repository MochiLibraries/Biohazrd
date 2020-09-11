namespace Biohazrd.Transformation
{
    partial class TypeTransformationBase
    {
        private TypeTransformationResult TransformPointerTypeReferenceChildren(TypeTransformationContext context, PointerTypeReference type)
        {
            TypeTransformationResult innerResult = TransformTypeRecursively(context, type.Inner);

            if (innerResult.IsChange(type.Inner))
            {
                return innerResult.WithType(type with
                {
                    Inner = innerResult.TypeReference
                });
            }
            else
            { return type; }
        }
    }
}
