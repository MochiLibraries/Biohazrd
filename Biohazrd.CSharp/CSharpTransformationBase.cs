using Biohazrd.Transformation;

namespace Biohazrd.CSharp
{
    public abstract class CSharpTransformationBase : TransformationBase
    {
        protected override TransformationResult Transform(TransformationContext context, TranslatedDeclaration declaration)
            => declaration switch
            {
                ConstantArrayTypeDeclaration constantArrayTypeDeclaration => TransformConstantArrayTypeDeclaration(context, constantArrayTypeDeclaration),
                SynthesizedLooseDeclarationsTypeDeclaration synthesizedTypeDeclaration => TransformSynthesizedLooseDeclarationsType(context, synthesizedTypeDeclaration),
                _ => base.Transform(context, declaration)
            };

        protected virtual TransformationResult TransformConstantArrayTypeDeclaration(TransformationContext context, ConstantArrayTypeDeclaration declaration)
            => TransformDeclaration(context, declaration);

        protected virtual TransformationResult TransformSynthesizedLooseDeclarationsType(TransformationContext context, SynthesizedLooseDeclarationsTypeDeclaration declaration)
            => TransformDeclaration(context, declaration);
    }
}
