using Biohazrd.Transformation;

namespace Biohazrd.CSharp
{
    public sealed class RemoveRemainingTypedefsTransformation : TransformationBase
    {
        protected override TransformationResult TransformTypedef(TransformationContext context, TranslatedTypedef declaration)
            => null;
    }
}
