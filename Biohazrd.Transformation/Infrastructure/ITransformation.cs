namespace Biohazrd.Transformation.Infrastructure
{
    public interface ITransformation
    {
        TransformationResult TransformRecursively(TransformationContext context, TranslatedDeclaration declaration);
    }
}
