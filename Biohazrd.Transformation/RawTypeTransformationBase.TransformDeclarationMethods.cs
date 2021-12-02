namespace Biohazrd.Transformation
{
    partial class RawTypeTransformationBase
    {
        private TransformationResult TransformConstantTypeReferences(TransformationContext context, TranslatedConstant declaration)
        {
            if (declaration.Type is null)
            { return declaration; }

            TypeTransformationResult result = TransformTypeRecursively(context, declaration.Type);

            if (result.IsChange(declaration.Type))
            {
                return declaration with
                {
                    Type = result.TypeReference,
                    Diagnostics = declaration.Diagnostics.AddRange(result.Diagnostics)
                };
            }
            else
            { return declaration; }
        }

        private TransformationResult TransformEnumTypeReferences(TransformationContext context, TranslatedEnum declaration)
        {
            TypeTransformationResult result = TransformTypeRecursively(context, declaration.UnderlyingType);

            if (result.IsChange(declaration.UnderlyingType))
            {
                return declaration with
                {
                    UnderlyingType = result.TypeReference,
                    Diagnostics = declaration.Diagnostics.AddRange(result.Diagnostics)
                };
            }
            else
            { return declaration; }
        }

        private TransformationResult TransformFunctionTypeReferences(TransformationContext context, TranslatedFunction declaration)
        {
            TypeTransformationResult result = TransformTypeRecursively(context, declaration.ReturnType);

            if (result.IsChange(declaration.ReturnType))
            {
                return declaration with
                {
                    ReturnType = result.TypeReference,
                    Diagnostics = declaration.Diagnostics.AddRange(result.Diagnostics)
                };
            }
            else
            { return declaration; }
        }

        private TransformationResult TransformParameterTypeReferences(TransformationContext context, TranslatedParameter declaration)
        {
            TypeTransformationResult result = TransformTypeRecursively(context, declaration.Type);

            if (result.IsChange(declaration.Type))
            {
                return declaration with
                {
                    Type = result.TypeReference,
                    Diagnostics = declaration.Diagnostics.AddRange(result.Diagnostics)
                };
            }
            else
            { return declaration; }
        }

        private TransformationResult TransformStaticFieldTypeReferences(TransformationContext context, TranslatedStaticField declaration)
        {
            TypeTransformationResult result = TransformTypeRecursively(context, declaration.Type);

            if (result.IsChange(declaration.Type))
            {
                return declaration with
                {
                    Type = result.TypeReference,
                    Diagnostics = declaration.Diagnostics.AddRange(result.Diagnostics)
                };
            }
            else
            { return declaration; }
        }

        private TransformationResult TransformBaseFieldTypeReferences(TransformationContext context, TranslatedBaseField declaration)
        {
            TypeTransformationResult result = TransformTypeRecursively(context, declaration.Type);

            if (result.IsChange(declaration.Type))
            {
                return declaration with
                {
                    Type = result.TypeReference,
                    Diagnostics = declaration.Diagnostics.AddRange(result.Diagnostics)
                };
            }
            else
            { return declaration; }
        }

        private TransformationResult TransformNormalFieldTypeReferences(TransformationContext context, TranslatedNormalField declaration)
        {
            TypeTransformationResult result = TransformTypeRecursively(context, declaration.Type);

            if (result.IsChange(declaration.Type))
            {
                return declaration with
                {
                    Type = result.TypeReference,
                    Diagnostics = declaration.Diagnostics.AddRange(result.Diagnostics)
                };
            }
            else
            { return declaration; }
        }

        private TransformationResult TransformTypedefTypeReferences(TransformationContext context, TranslatedTypedef declaration)
        {
            TypeTransformationResult result = TransformTypeRecursively(context, declaration.UnderlyingType);

            if (result.IsChange(declaration.UnderlyingType))
            {
                return declaration with
                {
                    UnderlyingType = result.TypeReference,
                    Diagnostics = declaration.Diagnostics.AddRange(result.Diagnostics)
                };
            }
            else
            { return declaration; }
        }
    }
}
