using Biohazrd.Transformation.Infrastructure;

namespace Biohazrd.Transformation
{
    partial class TypeTransformationBase
    {
        private TypeTransformationResult TransformPointerTypeReferenceChildren(TypeTransformationContext context, PointerTypeReference type)
        {
            TypeTransformationResult innerResult = TransformTypeRecursively(context, type.Inner);

            if (innerResult.IsChange(type.Inner))
            {
                return innerResult.WithType(type with
                {
                    Inner = innerResult.TypeReference
                });
            }
            else
            { return type; }
        }

        private TypeTransformationResult TransformFunctionPointerTypeReferenceChildren(TypeTransformationContext context, FunctionPointerTypeReference type)
        {
            // Transform return type
            TypeTransformationResult newReturnType = TransformTypeRecursively(context, type.ReturnType);

            // Transform parameters
            TypeArrayTransformHelper newParameterTypes = new(type.ParameterTypes);

            foreach (TypeReference parameterType in type.ParameterTypes)
            { newParameterTypes.Add(TransformTypeRecursively(context, parameterType)); }

            newParameterTypes.Finish();

            // Create the result
            TypeTransformationResult result;

            // Create a new funciton pointer if there were changes
            if (newReturnType.IsChange(type.ReturnType) || newParameterTypes.CollectionWasChanged)
            {
                result = type with
                {
                    ReturnType = newReturnType.TypeReference,
                    ParameterTypes = newParameterTypes.MoveToImmutable()
                };
            }
            else
            { result = type; }

            // Add any diagnostics to the result
            result = result.AddDiagnostics(newReturnType.Diagnostics);
            result = result.AddDiagnostics(newParameterTypes.GetDiagnostics());

            // Return the result
            return result;
        }
    }
}
