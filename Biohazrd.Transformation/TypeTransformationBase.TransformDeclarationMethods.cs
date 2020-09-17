using System.ComponentModel;

namespace Biohazrd.Transformation
{
    partial class TypeTransformationBase
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformEnum(TransformationContext context, TranslatedEnum declaration)
        {
            context = context.Add(declaration);
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

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            context = context.Add(declaration);
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

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformParameter(TransformationContext context, TranslatedParameter declaration)
        {
            context = context.Add(declaration);
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

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformStaticField(TransformationContext context, TranslatedStaticField declaration)
        {
            context = context.Add(declaration);
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

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformBaseField(TransformationContext context, TranslatedBaseField declaration)
        {
            context = context.Add(declaration);
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

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformNormalField(TransformationContext context, TranslatedNormalField declaration)
        {
            context = context.Add(declaration);
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

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformTypedef(TransformationContext context, TranslatedTypedef declaration)
        {
            context = context.Add(declaration);
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

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected override TransformationResult TransformVTableEntry(TransformationContext context, TranslatedVTableEntry declaration)
        {
            context = context.Add(declaration);
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

        //===========================================================================================================================================
        // Declarations which have no type references
        //===========================================================================================================================================
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
            => base.TransformDeclaration(context, declaration);

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformEnumConstant(TransformationContext context, TranslatedEnumConstant declaration)
            => base.TransformEnumConstant(context, declaration);

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformRecord(TransformationContext context, TranslatedRecord declaration)
            => base.TransformRecord(context, declaration);

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformUndefinedRecord(TransformationContext context, TranslatedUndefinedRecord declaration)
            => base.TransformUndefinedRecord(context, declaration);

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformUnsupportedDeclaration(TransformationContext context, TranslatedUnsupportedDeclaration declaration)
            => base.TransformUnsupportedDeclaration(context, declaration);

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformVTable(TransformationContext context, TranslatedVTable declaration)
            => base.TransformVTable(context, declaration);

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformField(TransformationContext context, TranslatedField declaration)
            => base.TransformField(context, declaration);

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformUnimplementedField(TransformationContext context, TranslatedUnimplementedField declaration)
            => base.TransformUnimplementedField(context, declaration);

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected sealed override TransformationResult TransformVTableField(TransformationContext context, TranslatedVTableField declaration)
            => base.TransformVTableField(context, declaration);
    }
}
