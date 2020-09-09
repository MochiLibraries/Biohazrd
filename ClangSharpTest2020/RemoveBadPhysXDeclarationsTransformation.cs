using Biohazrd;
using Biohazrd.Transformation;

namespace ClangSharpTest2020
{
    public sealed class RemoveBadPhysXDeclarationsTransformation : TransformationBase
    {
        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            // PxRepXInstantiationArg::operator= is never actually defined in PhysX and as such cannot be called.
            if (context.ParentDeclaration?.Name == "PxRepXInstantiationArgs" && declaration.IsOperatorOverload)
            { return null; }

            return declaration;
        }
    }
}
