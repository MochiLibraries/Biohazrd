namespace Biohazrd.Transformation
{
    partial class TypeTransformationBase
    {
        protected virtual TypeTransformationResult TransformTypeReference(TypeTransformationContext context, TypeReference type)
            => type;

        protected virtual TypeTransformationResult TransformClangTypeReference(TypeTransformationContext context, ClangTypeReference type)
            => TransformTypeReference(context, type);

        protected virtual TypeTransformationResult TransformFunctionPointerTypeReference(TypeTransformationContext context, FunctionPointerTypeReference type)
            => TransformTypeReference(context, type);

        protected virtual TypeTransformationResult TransformPointerTypeReference(TypeTransformationContext context, PointerTypeReference type)
            => TransformTypeReference(context, type);

        protected virtual TypeTransformationResult TransformTranslatedTypeReference(TypeTransformationContext context, TranslatedTypeReference type)
            => TransformTypeReference(context, type);

        protected virtual TypeTransformationResult TransformVoidTypeReference(TypeTransformationContext context, VoidTypeReference type)
            => TransformTypeReference(context, type);
    }
}
