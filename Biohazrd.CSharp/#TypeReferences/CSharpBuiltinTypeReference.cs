using Biohazrd.Transformation;
using Biohazrd.Transformation.Infrastructure;

namespace Biohazrd.CSharp
{
    public sealed record CSharpBuiltinTypeReference : TypeReference, ICustomTypeReference
    {
        public CSharpBuiltinType Type { get; }

        internal CSharpBuiltinTypeReference(CSharpBuiltinType type)
            => Type = type;

        public override string ToString()
            => Type.ToString();

        TypeTransformationResult ICustomTypeReference.TransformChildren(ITypeTransformation transformation, TypeTransformationContext context)
            => this;
    }
}
