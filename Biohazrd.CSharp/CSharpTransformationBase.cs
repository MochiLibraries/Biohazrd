using Biohazrd.Transformation;

namespace Biohazrd.CSharp
{
    public abstract class CSharpTransformationBase : TransformationBase
    {
        protected override TransformationResult Transform(TransformationContext context, TranslatedDeclaration declaration)
            => declaration switch
            {
                SynthesizedLooseDeclarationsType synthesizedTypeDeclaration => TransformSynthesizedLooseDeclarationsType(context, synthesizedTypeDeclaration),
                _ => base.Transform(context, declaration)
            };

        protected virtual TransformationResult TransformSynthesizedLooseDeclarationsType(TransformationContext context, SynthesizedLooseDeclarationsType declaration)
            => TransformDeclaration(context, declaration);
    }
}
