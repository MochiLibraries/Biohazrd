using Biohazrd.Transformation;

namespace Biohazrd.CSharp
{
    public abstract class CSharpTypeTransformationBase : TypeTransformationBase
    {
        protected override TypeTransformationResult TransformType(TypeTransformationContext context, TypeReference type)
            => type switch
            {
                CSharpBuiltinTypeReference cSharpBuiltinType => TransformCSharpBuiltinTypeReference(context, cSharpBuiltinType),
                _ => base.TransformType(context, type)
            };

        protected virtual TypeTransformationResult TransformCSharpBuiltinTypeReference(TypeTransformationContext context, CSharpBuiltinTypeReference type)
            => TransformTypeReference(context, type);
    }
}
