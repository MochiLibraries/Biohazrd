using Biohazrd.Transformation;
using System;

namespace Biohazrd.CSharp
{
    [Obsolete("This transformation has been superseded by better handling of typedefs during the output stage and should no longer be used.")]
    public sealed class RemoveRemainingTypedefsTransformation : TransformationBase
    {
        protected override TransformationResult TransformTypedef(TransformationContext context, TranslatedTypedef declaration)
            => null;
    }
}
