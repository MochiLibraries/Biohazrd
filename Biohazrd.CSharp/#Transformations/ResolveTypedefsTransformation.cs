using Biohazrd.Transformation;

namespace Biohazrd.CSharp
{
    public sealed class ResolveTypedefsTransformation : TypeTransformationBase
    {
        protected override TypeTransformationResult TransformTranslatedTypeReference(TypeTransformationContext context, TranslatedTypeReference type)
        {
            if (type.TryResolve(context.Library) is TranslatedTypedef typedef)
            { return typedef.UnderlyingType; }
            else
            { return type; }
        }

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
            => new RemoveTypedefsTransformation().Transform(library);

        private sealed class RemoveTypedefsTransformation : TransformationBase
        {
            protected override TransformationResult TransformTypedef(TransformationContext context, TranslatedTypedef declaration)
                => null;
        }
    }
}
