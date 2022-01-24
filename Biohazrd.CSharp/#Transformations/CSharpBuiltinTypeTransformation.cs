using Biohazrd.Transformation;
using System;
using System.ComponentModel;

namespace Biohazrd.CSharp
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("This transformation was merged into CSharpTypeReductionTransformation and is now a no-op. Its usage should be removed.")]
    public sealed class CSharpBuiltinTypeTransformation : TypeTransformationBase
    { }
}
