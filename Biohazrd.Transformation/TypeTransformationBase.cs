using System.Collections.Immutable;

namespace Biohazrd.Transformation
{
    public partial class TypeTransformationBase : TransformationBase
    {
        /// <summary>If true, a type transformation will run more than once until the transformation fails to transform the type reference.</summary>
        /// <remarks>Persistent transformation only occurs when the actual type reference changes. Persistent transformation does not occur if only diagnostics are added.</remarks>
        protected virtual bool PersistentTypeTransformation => true;

        private TypeTransformationResult TransformTypeRecursively(TypeTransformationContext context, TypeReference type)
        {
            // Transform this type
            TypeTransformationResult result = TransformType(context, type);

            // Persistently transform if applicable
            if (PersistentTypeTransformation)
            {
                TypeReference previousResult = type;

                while (result.TypeReference != previousResult)
                {
                    previousResult = result.TypeReference;
                    ImmutableArray<TranslationDiagnostic> previousDiagnostics = result.Diagnostics;

                    result = TransformType(context, previousResult);

                    // Preserve diagnostics emitted during the previous iteration
                    result = result.AddDiagnostics(previousDiagnostics);
                }
            }

            // Transform this type's children
            TypeTransformationResult recursiveResult = TransformTypeChildren(context, result.TypeReference);

            // Append diagnostics from the first transformation if there are any and return the result
            return recursiveResult.AddDiagnostics(result.Diagnostics);
        }

        protected virtual TypeTransformationResult TransformType(TypeTransformationContext context, TypeReference type)
            => type switch
            {
                TranslatedTypeReference translatedType => TransformTranslatedTypeReference(context, translatedType),
                PointerTypeReference pointerType => TransformPointerTypeReference(context, pointerType),
                ClangTypeReference clangType => TransformClangTypeReference(context, clangType),
                // Fallback type
                TypeReference => TransformUnknownTypeReference(context, type)
            };

        protected virtual TypeTransformationResult TransformTypeChildren(TypeTransformationContext context, TypeReference type)
            => type switch
            {
                PointerTypeReference pointerType => TransformPointerTypeReferenceChildren(context.Add(type), pointerType),
                TypeReference => type
            };

        protected virtual TypeTransformationResult TransformUnknownTypeReference(TypeTransformationContext context, TypeReference type)
            => TransformTypeReference(context, type);
    }
}
