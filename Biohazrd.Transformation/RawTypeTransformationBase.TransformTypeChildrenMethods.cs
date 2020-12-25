using Biohazrd.Transformation.Infrastructure;

namespace Biohazrd.Transformation
{
    partial class RawTypeTransformationBase
    {
        private TypeTransformationResult TransformPointerTypeReferenceChildren(TypeTransformationContext context, PointerTypeReference type)
        {
            DiagnosticAccumulator diagnostics = new();
            SingleTypeTransformHelper newInner = new(type.Inner, ref diagnostics);

            // Transform inner
            newInner.SetValue(TransformTypeRecursively(context, type.Inner));

            // Create the result
            TypeTransformationResult result;

            if (newInner.WasChanged)
            {
                result = type with
                {
                    Inner = newInner.NewValue
                };
            }
            else
            { result = type; }

            // Add any diagnostics to the result
            result = result.AddDiagnostics(diagnostics.MoveToImmutable());

            // Return the result
            return result;
        }

        private TypeTransformationResult TransformFunctionPointerTypeReferenceChildren(TypeTransformationContext context, FunctionPointerTypeReference type)
        {
            DiagnosticAccumulator diagnostics = new();
            SingleTypeTransformHelper newReturnType = new(type.ReturnType, ref diagnostics);
            TypeArrayTransformHelper newParameterTypes = new(type.ParameterTypes, ref diagnostics);

            // Transform return type
            newReturnType.SetValue(TransformTypeRecursively(context, type.ReturnType));

            // Transform parameters
            foreach (TypeReference parameterType in type.ParameterTypes)
            { newParameterTypes.Add(TransformTypeRecursively(context, parameterType)); }

            newParameterTypes.Finish();

            // Create the result
            TypeTransformationResult result;

            if (newReturnType.WasChanged || newParameterTypes.WasChanged)
            {
                result = type with
                {
                    ReturnType = newReturnType.NewValue,
                    ParameterTypes = newParameterTypes.MoveToImmutable()
                };
            }
            else
            { result = type; }

            // Add any diagnostics to the result
            result = result.AddDiagnostics(diagnostics.MoveToImmutable());

            // Return the result
            return result;
        }
    }
}
