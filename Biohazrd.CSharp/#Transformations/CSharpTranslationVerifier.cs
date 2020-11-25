using Biohazrd.Expressions;
using Biohazrd.Transformation;
using System.Linq;

namespace Biohazrd.CSharp
{
    //TODO: Some of these verifications are not specific to C#, it might be a good idea to pull them out into their own thing.
    public sealed class CSharpTranslationVerifier : CSharpTransformationBase
    {
        private CSharpTranslationVerifierPass2 Pass2 = new();

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
            => Pass2.Transform(library);

        protected override TransformationResult TransformDeclaration(TransformationContext context, Biohazrd.TranslatedDeclaration declaration)
        {
            // If this declaration is at the root, ensure we're using an access level that's valid at this scope
            if (context.ParentDeclaration is null && !declaration.Accessibility.IsAllowedInNamespaceScope())
            {
                declaration = declaration with
                {
                    Diagnostics = declaration.Diagnostics.Add
                    (
                        Severity.Warning,
                        $"Declaration translated as {declaration.Accessibility.ToCSharpKeyword()}, but it will be translated into a file/namespace scope. Accessibility forced to internal."
                    ),
                    Accessibility = AccessModifier.Internal
                };
            }

            // Currently everything is translated as structs and static classes, neither of which support protected.
            switch (declaration.Accessibility)
            {
                case AccessModifier.Protected:
                case AccessModifier.ProtectedAndInternal:
                case AccessModifier.ProtectedOrInternal:
                    declaration = declaration with
                    {
                        Diagnostics = declaration.Diagnostics.Add
                        (
                            Severity.Warning,
                            $"Declaration translated as {declaration.Accessibility.ToCSharpKeyword()}, but protected isn't supported yet. Accessibility forced to internal."
                        ),
                        Accessibility = AccessModifier.Internal
                    };
                    break;
            }

            return base.TransformDeclaration(context, declaration);
        }

        protected override TransformationResult TransformUnknownDeclarationType(TransformationContext context, Biohazrd.TranslatedDeclaration declaration)
            => base.TransformUnknownDeclarationType(context, declaration);

        protected override TransformationResult TransformEnum(TransformationContext context, TranslatedEnum declaration)
        {
            // If the enum will be translated as a C# enum, the underlying type must be supported by C#.
            // If the type is not supported, we force it to be translated as loose constants and add a warning to it.
            if (!declaration.TranslateAsLooseConstants && declaration.UnderlyingType is not CSharpBuiltinTypeReference { Type: { IsValidUnderlyingEnumType: true } })
            {
                declaration = declaration with
                {
                    TranslateAsLooseConstants = true,
                    Diagnostics = declaration.Diagnostics.Add
                    (
                        Severity.Warning,
                        $"Enum declaration had an underlying type of '{declaration.UnderlyingType}', which is not supported by C#."
                    )
                };
            }

            return base.TransformEnum(context, declaration);
        }

        protected override TransformationResult TransformEnumConstant(TransformationContext context, TranslatedEnumConstant declaration)
        {
            if (context.ParentDeclaration is not TranslatedEnum)
            { declaration = declaration.WithError($"Enum constants are not valid outside of a enum context."); }

            return base.TransformEnumConstant(context, declaration);
        }

        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            //TODO: Verify return type is compatible
            //TODO: We might want to check if they can be resolved in an extra pass due to BrokenDeclarationExtractor.
            if (!context.IsValidFieldOrMethodContext())
            { declaration = declaration.WithError("Loose functions are not supported in C#."); }

            return base.TransformFunction(context, declaration);
        }

        protected override TransformationResult TransformParameter(TransformationContext context, TranslatedParameter declaration)
        {
            //TODO: Verify type is compatible
            if (context.ParentDeclaration is not TranslatedFunction)
            { declaration = declaration.WithError("Function parameters are not valid outside of a function context."); }

            // Verify default parameter value is compatible
            switch (declaration.DefaultValue)
            {
                case StringConstant:
                    return declaration with
                    {
                        DefaultValue = null,
                        Diagnostics = declaration.Diagnostics.Add(Severity.Warning, "String constants are not supported as default parameter values.")
                    };
                case UnsupportedConstantExpression:
                    // No diagnostic here, it was already emitted during the initial translation
                    return declaration with
                    {
                        DefaultValue = null
                    };
                default:
                {
                    // This can happen if a C# built-in type was replaced with a different type
                    // (Such as a type being replaced with a convienience wrapper.)
                    static bool TypeCanHaveDefaultInCSharp(TransformationContext context, TypeReference type)
                    {
                        if (type is PointerTypeReference)
                        { return true; }

                        if (type is CSharpBuiltinTypeReference)
                        { return true; }

                        if (type is TranslatedTypeReference translatedType && translatedType.TryResolve(context.Library) is TranslatedEnum)
                        { return true; }

                        return false;
                    }

                    if (declaration.DefaultValue is not null && !TypeCanHaveDefaultInCSharp(context, declaration.Type))
                    {
                        return declaration with
                        {
                            DefaultValue = null,
                            Diagnostics = declaration.Diagnostics.Add(Severity.Warning, $"Default parameter values are not supported for this parameter's type.")
                        };
                    }

                    // Default parameter value is allowed
                    return base.TransformParameter(context, declaration);
                }
            }
        }

        protected override TransformationResult TransformRecord(TransformationContext context, TranslatedRecord declaration)
        {
            if (declaration.UnsupportedMembers.Count > 0)
            { declaration = declaration.WithWarning("Records with unsupported members may not be translated correctly."); }

            if (declaration.VTable is null && declaration.VTableField is not null)
            { declaration = declaration.WithError("Records should not have a VTable field without a VTable."); }
            else if (declaration.VTable is not null && declaration.VTableField is null)
            { declaration = declaration.WithError("Records should not have a VTable without a VTable field."); }

            return base.TransformRecord(context, declaration);
        }

        protected override TransformationResult TransformBitField(TransformationContext context, TranslatedBitField declaration)
        {
            switch (declaration.Type)
            {
                // Integral C# built-in types are allowed
                case CSharpBuiltinTypeReference { Type: { IsIntegral: true } }:
                    return base.TransformBitField(context, declaration);
                // Integral enums are allowed
                case TranslatedTypeReference typeReference:
                    if (typeReference.TryResolve(context.Library) is TranslatedEnum { UnderlyingType: CSharpBuiltinTypeReference { Type: { IsIntegral: true } } })
                    { return base.TransformBitField(context, declaration); }
                    break;
            }

            return declaration.WithError($"Bit fields must by typed by an integral C# built-in type or an enum with an integral underlying type. {declaration.Type} is neither.");
        }

        protected override TransformationResult TransformStaticField(TransformationContext context, TranslatedStaticField declaration)
        {
            //TODO: Verify type is compatible
            if (!context.IsValidFieldOrMethodContext())
            { declaration = declaration.WithError("Loose fields are not supported in C#."); }

            return base.TransformStaticField(context, declaration);
        }

        protected override TransformationResult TransformTypedef(TransformationContext context, TranslatedTypedef declaration)
            // Typedefs have no impact on the output, so there's nothing to verify.
            => base.TransformTypedef(context, declaration);

        protected override TransformationResult TransformUndefinedRecord(TransformationContext context, TranslatedUndefinedRecord declaration)
            => base.TransformUndefinedRecord(context, declaration);

        protected override TransformationResult TransformUnsupportedDeclaration(TransformationContext context, TranslatedUnsupportedDeclaration declaration)
        {
            if (!declaration.Diagnostics.All(d => d.IsError))
            { declaration = declaration.WithError($"Declarations not supported by Biohazrd cannot be translated to C#."); }

            return base.TransformUnsupportedDeclaration(context, declaration);
        }

        protected override TransformationResult TransformVTable(TransformationContext context, TranslatedVTable declaration)
        {
            if (context.ParentDeclaration is not TranslatedRecord recordParent)
            { declaration = declaration.WithError("VTables must be the child of a record."); }
            else if (!ReferenceEquals(recordParent.VTable, declaration))
            {
                if (recordParent.VTable is null)
                { declaration = declaration.WithError("VTables must be associated with the record as VTables."); }
                else
                { declaration = declaration.WithError("Multiple VTables are not yet supported."); }
            }

            return base.TransformVTable(context, declaration);
        }

        protected override TransformationResult TransformField(TransformationContext context, TranslatedField declaration)
        {
            if (!context.IsValidFieldOrMethodContext())
            { declaration = declaration.WithError("Loose fields are not supported in C#."); }

            // Fields in C++ can have the same name as their enclosing type, but this isn't allowed in C# (it results in CS0542)
            // When we encounter such fields, we rename them to avoid the error.
            if (context.ParentDeclaration?.Name == declaration.Name)
            {
                string newName = declaration.Name;

                do
                { newName += "_"; }
                while (context.Parent.Any(d => d.Name == newName));

                declaration = declaration with
                {
                    Diagnostics = declaration.Diagnostics.Add(Severity.Warning, $"Field has the same name as its enclosing type, renamed to '{newName}' to avoid conflict.")
                };
            }

            return base.TransformField(context, declaration);
        }

        protected override TransformationResult TransformBaseField(TransformationContext context, TranslatedBaseField declaration)
        {
            // Do not error if our parent isn't a record, that's handled in TransformField
            if (context.ParentDeclaration is TranslatedRecord recordParent)
            {
                if (recordParent.NonVirtualBaseField is null)
                { declaration = declaration.WithError("Base fields must be associated with the record as the non-virtual base field."); }
                else if (!ReferenceEquals(recordParent.NonVirtualBaseField, declaration))
                { declaration = declaration.WithError("Multiple bases are not yet supported."); }
            }

            return base.TransformBaseField(context, declaration);
        }

        protected override TransformationResult TransformNormalField(TransformationContext context, TranslatedNormalField declaration)
            //TODO: Verify type is compatible
            => base.TransformNormalField(context, declaration);
        protected override TransformationResult TransformUnimplementedField(TransformationContext context, TranslatedUnimplementedField declaration)
            => base.TransformUnimplementedField(context, declaration.WithWarning($"{declaration.Kind} fields are not yet supported."));
        protected override TransformationResult TransformVTableField(TransformationContext context, TranslatedVTableField declaration)
            => base.TransformVTableField(context, declaration);
    }
}
