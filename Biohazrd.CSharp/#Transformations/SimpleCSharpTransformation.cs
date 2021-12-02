using Biohazrd.Transformation;
using Biohazrd.Transformation.Common;

namespace Biohazrd.CSharp
{
    public record SimpleCSharpTransformation : SimpleTransformation
    {
        public override TranslatedLibrary Transform(TranslatedLibrary library)
            => new CSharpTransformation(this).Transform(library);

        public TransformationMethod<ConstantArrayTypeDeclaration>? TransformConstantArrayType { get; init; }
        public TransformationMethod<SynthesizedLooseDeclarationsTypeDeclaration>? TransformSynthesizedLooseDeclarationsType { get; init; }

        protected class CSharpTransformation : Transformation
        {
            new protected readonly SimpleCSharpTransformation Parent;

            public CSharpTransformation(SimpleCSharpTransformation parent)
                : base(parent)
                => Parent = parent;

            protected override TransformationResult Transform(TransformationContext context, TranslatedDeclaration declaration)
                => declaration switch
                {
                    ConstantArrayTypeDeclaration constantArrayTypeDeclaration => TransformConstantArrayType(context, constantArrayTypeDeclaration),
                    SynthesizedLooseDeclarationsTypeDeclaration synthesizedTypeDeclaration => TransformSynthesizedLooseDeclarationsType(context, synthesizedTypeDeclaration),
                    _ => base.Transform(context, declaration)
                };

            protected TransformationResult TransformConstantArrayType(TransformationContext context, ConstantArrayTypeDeclaration declaration)
                => Parent.TransformConstantArrayType is not null ? Parent.TransformConstantArrayType(context, declaration) : TransformDeclaration(context, declaration);
            protected TransformationResult TransformSynthesizedLooseDeclarationsType(TransformationContext context, SynthesizedLooseDeclarationsTypeDeclaration declaration)
                => Parent.TransformSynthesizedLooseDeclarationsType is not null ? Parent.TransformSynthesizedLooseDeclarationsType(context, declaration) : TransformDeclaration(context, declaration);
        }
    }
}
