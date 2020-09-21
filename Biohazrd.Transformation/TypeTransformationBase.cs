namespace Biohazrd.Transformation
{
    public abstract class TypeTransformationBase : RawTypeTransformationBase
    {
        protected override TypeTransformationResult TransformType(TypeTransformationContext context, TypeReference type)
            => type switch
            {
                ClangTypeReference clangType => TransformClangTypeReference(context, clangType),
                FunctionPointerTypeReference functionPointer => TransformFunctionPointerTypeReference(context, functionPointer),
                PointerTypeReference pointerType => TransformPointerTypeReference(context, pointerType),
                TranslatedTypeReference translatedType => TransformTranslatedTypeReference(context, translatedType),
                VoidTypeReference voidType => TransformVoidTypeReference(context, voidType),
                // Fallback type
                TypeReference => TransformUnknownTypeReference(context, type)
            };

        protected virtual TypeTransformationResult TransformTypeReference(TypeTransformationContext context, TypeReference type)
            => type;

        protected virtual TypeTransformationResult TransformUnknownTypeReference(TypeTransformationContext context, TypeReference type)
            => TransformTypeReference(context, type);

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
