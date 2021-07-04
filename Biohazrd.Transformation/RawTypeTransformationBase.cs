using Biohazrd.Transformation.Infrastructure;
using System.Collections.Immutable;

namespace Biohazrd.Transformation
{
    public abstract partial class RawTypeTransformationBase : RawTransformationBase, ITypeTransformation
    {
        /// <summary>If true, a type transformation will run more than once until the transformation fails to transform the type reference.</summary>
        /// <remarks>Persistent transformation only occurs when the actual type reference changes. Persistent transformation does not occur if only diagnostics are added.</remarks>
        protected virtual bool PersistentTypeTransformation => true;

        protected TypeTransformationResult TransformTypeRecursively(TypeTransformationContext context, TypeReference type)
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

        protected sealed override TransformationResult Transform(TransformationContext context, TranslatedDeclaration declaration)
            => declaration switch
            {
                ICustomTranslatedDeclaration customDeclaration => customDeclaration.TransformTypeChildren(this, context.Add(declaration)),
                TranslatedEnum enumDeclaration => TransformEnumTypeReferences(context.Add(declaration), enumDeclaration),
                TranslatedFunction functionDeclaration => TransformFunctionTypeReferences(context.Add(declaration), functionDeclaration),
                TranslatedParameter parameterDeclaration => TransformParameterTypeReferences(context.Add(declaration), parameterDeclaration),
                TranslatedStaticField staticFieldDeclaration => TransformStaticFieldTypeReferences(context.Add(declaration), staticFieldDeclaration),
                TranslatedBaseField baseFieldDeclaration => TransformBaseFieldTypeReferences(context.Add(declaration), baseFieldDeclaration),
                TranslatedNormalField normalFieldDeclaration => TransformNormalFieldTypeReferences(context.Add(declaration), normalFieldDeclaration),
                TranslatedTypedef typedefDeclaration => TransformTypedefTypeReferences(context.Add(declaration), typedefDeclaration),
                TranslatedDeclaration => declaration
            };

        TypeTransformationResult ITypeTransformation.TransformTypeRecursively(TypeTransformationContext context, TypeReference type)
            => TransformTypeRecursively(context, type);

        protected abstract TypeTransformationResult TransformType(TypeTransformationContext context, TypeReference type);

        protected virtual TypeTransformationResult TransformTypeChildren(TypeTransformationContext context, TypeReference type)
            => type switch
            {
                ICustomTypeReference customTypeReference => customTypeReference.TransformChildren(this, context.Add(type)),
                FunctionPointerTypeReference functionPointer => TransformFunctionPointerTypeReferenceChildren(context.Add(type), functionPointer),
                PointerTypeReference pointerType => TransformPointerTypeReferenceChildren(context.Add(type), pointerType),
                TypeReference => type
            };
    }
}
