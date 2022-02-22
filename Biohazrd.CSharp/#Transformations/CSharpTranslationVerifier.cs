using Biohazrd.CSharp.Metadata;
using Biohazrd.CSharp.Trampolines;
using Biohazrd.Expressions;
using Biohazrd.Transformation;
using Biohazrd.Transformation.Infrastructure;
using ClangSharp;
using ClangSharp.Pathogen;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;

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

        protected override TransformationResult TransformConstant(TransformationContext context, TranslatedConstant declaration)
        {
            if (!context.IsValidFieldOrMethodContext())
            { return declaration.WithError("Loose constants are not supported in C#."); }

            if (declaration.Value is UnsupportedConstantExpression unsupportedConstant)
            { return declaration.WithError($"Constant is not supported: {unsupportedConstant.Message}"); }

            if (declaration.Type is CSharpBuiltinTypeReference)
            { return declaration; }
            else if (declaration.Type is null)
            {
                // If the type of the constant cannot be inferred as a C# type, it isn't supported as a constant in C#
                if (declaration.Value.InferType() is null)
                { return declaration.WithError($"Constants with {declaration.Value.GetType().Name} values are not supported in C#."); }
                else
                { return declaration; }
            }
            else if (declaration.Type is TranslatedTypeReference typeReference)
            {
                TranslatedDeclaration? typeDeclaration = typeReference.TryResolve(context.Library);

                if (typeDeclaration is null)
                { return declaration.WithError($"Constant's type '{typeReference}' failed to resolve."); }
                else if (typeDeclaration is TranslatedEnum)
                { return declaration; }
                else
                { return declaration.WithError($"Constants of type '{typeDeclaration}' are not supported as C# constants."); }
            }
            else
            { return declaration.WithError($"Constants of type '{declaration.Type.GetType().Name}' are not supported as C# constants."); }
        }

        protected override TransformationResult TransformEnum(TransformationContext context, TranslatedEnum declaration)
        {
            bool canBeFields = context.IsValidFieldOrMethodContext();
            bool canBeEnum = declaration.UnderlyingType is CSharpBuiltinTypeReference { Type: { IsValidUnderlyingEnumType: true } };

            // If this enum can't be an enum and can't be loose fields, it becomes loose fields inside a loose declaration wrapper
            if (!canBeFields && !canBeEnum)
            {
                return new SynthesizedLooseDeclarationsTypeDeclaration(declaration.File)
                {
                    Name = declaration.Name,
                    Namespace = declaration.Namespace,
                    Accessibility = declaration.Accessibility,
                    Members = ImmutableList.Create<TranslatedDeclaration>
                    (
                        declaration with
                        {
                            TranslateAsLooseConstants = true,
                            Diagnostics = declaration.Diagnostics.Add
                            (
                                Severity.Warning,
                                $"Enums can't be translated as  C# enum or as loose constants, and was wrapped in loose declaration container."
                            )
                        }
                    )
                };
            }
            // If this enum will be translated as loose constants, it must be in a valid field context
            else if (declaration.TranslateAsLooseConstants && !canBeFields)
            {
                return declaration with
                {
                    TranslateAsLooseConstants = false,
                    Diagnostics = declaration.Diagnostics.Add(Severity.Warning, $"Enums outside of a field declaration context cannot be translated as loose constants.")
                };
            }
            // If the enum will be translated as a C# enum, the underlying type must be supported by C#.
            // If the type is not supported, we force it to be translated as loose constants and add a warning to it.
            else if (!declaration.TranslateAsLooseConstants && !canBeEnum)
            {
                return declaration with
                {
                    TranslateAsLooseConstants = true,
                    Diagnostics = declaration.Diagnostics.Add
                    (
                        Severity.Warning,
                        $"Enum declaration had an underlying type of '{declaration.UnderlyingType}', which is not supported by C#."
                    )
                };
            }
            else
            { return declaration; }
        }

        protected override TransformationResult TransformEnumConstant(TransformationContext context, TranslatedEnumConstant declaration)
        {
            if (context.ParentDeclaration is not TranslatedEnum)
            { declaration = declaration.WithError($"Enum constants are not valid outside of a enum context."); }

            return base.TransformEnumConstant(context, declaration);
        }

        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            // Validate the trampolines
            if (!declaration.Metadata.TryGet(out TrampolineCollection trampolines))
            { declaration = declaration.WithWarning($"Function does not have trampolines which will soon be deprecated, use {nameof(CreateTrampolinesTransformation)} to add them."); }
            else if (trampolines.__OriginalFunction is null)
            { declaration = declaration.WithWarning($"Function has trampolines but they were defaulted. Trampolines should be added via {nameof(CreateTrampolinesTransformation)}."); }
            else
            {
                TranslatedFunction original = trampolines.__OriginalFunction;

                // Validate name did not change too late
                if (declaration.Name != original.Name)
                { declaration = declaration.WithWarning($"Function's name was changed from '{original.Name}' to '{declaration.Name}' after trampolines were added. This change will not be reflected in the output."); }

                // Validate return type did not change too late
                if (declaration.ReturnType != original.ReturnType)
                { declaration = declaration.WithWarning($"Function's return type was changed from '{original.ReturnType}' to '{declaration.ReturnType}' after trampolines were added. This change will not be reflected in the output."); }

                // Validate the parameter count did not change too late
                // (This situation is somewhat nonsensical, but it *is* possible so we need ot handle it somehow.)
                if (declaration.Parameters.Length != original.Parameters.Length)
                { declaration = declaration.WithWarning($"The number of parameters changed from {original.Parameters.Length} to {declaration.Parameters.Length} after trampolines were added. This change will not be reflected in the output."); }
                else
                {
                    // Validate the parameter names and types did not change too late
                    ArrayTransformHelper<TranslatedParameter> verifiedParameters = new(declaration.Parameters);
                    for (int i = 0; i < declaration.Parameters.Length; i++)
                    {
                        TranslatedParameter parameter = declaration.Parameters[i];
                        TranslatedParameter originalParameter = original.Parameters[i];

                        if (parameter.Type != originalParameter.Type)
                        { parameter = parameter.WithWarning($"Parameter's type was changed from '{originalParameter.Type}' to '{parameter.Type}' after trampolines were added. This change will not be reflected in the output."); }

                        if (parameter.Name != originalParameter.Name)
                        { parameter = parameter.WithWarning($"Parameter's name was changed from '{originalParameter.Name}' to '{parameter.Name}' after trampolines were added. This change will not be reflected in the output."); }

                        verifiedParameters.Add(parameter);
                    }

                    if (verifiedParameters.WasChanged)
                    {
                        declaration = declaration with
                        {
                            Parameters = verifiedParameters.MoveToImmutable()
                        };
                    }
                }

                // Validate the trampoline graph is complete
                // This has to be done iteratively so if we have A -> B -> (missing) and A is checked before B we will remove both.
                bool foundBrokenLinks;
                do
                {
                    foundBrokenLinks = false;

                    // Validate primary trampoline
                    Trampoline primary = trampolines.PrimaryTrampoline;
                    if (!primary.IsNativeFunction && !trampolines.Contains(primary.Target))
                    {
                        trampolines = trampolines with { PrimaryTrampoline = trampolines.NativeFunction };
                        declaration = declaration with
                        {
                            Diagnostics = declaration.Diagnostics.Add(Severity.Warning, $"Trampoline '{trampolines.PrimaryTrampoline.Description}' referenced missing trampoline '{primary.Target.Description}' and was removed."),
                            Metadata = declaration.Metadata.Set(trampolines)
                        };

                        foundBrokenLinks = true;
                    }

                    // Verify secondary trampolines
                    foreach (Trampoline trampoline in trampolines.SecondaryTrampolines)
                    {
                        if (trampoline.IsNativeFunction)
                        {
                            Debug.Fail("Secondary trampolines should not be native trampolines!");
                            continue;
                        }

                        if (!trampolines.Contains(trampoline.Target))
                        {
                            trampolines = trampolines with
                            {
                                SecondaryTrampolines = trampolines.SecondaryTrampolines.Remove(trampoline, ReferenceEqualityComparer.Instance)
                            };

                            declaration = declaration with
                            {
                                Diagnostics = declaration.Diagnostics.Add(Severity.Warning, $"Trampoline '{trampolines.PrimaryTrampoline.Description}' referenced missing trampoline '{trampoline.Description}' and was removed."),
                                Metadata = declaration.Metadata.Set(trampolines)
                            };

                            foundBrokenLinks = true;
                        }
                    }
                } while (foundBrokenLinks);
            }

            //TODO: Verify return type is compatible
            //TODO: We might want to check if they can be resolved in an extra pass due to BrokenDeclarationExtractor.
            if (!context.IsValidFieldOrMethodContext())
            { declaration = declaration.WithError("Loose functions are not supported in C#."); }

            if (declaration.IsVirtual && declaration.Metadata.Has<SetLastErrorFunction>())
            { declaration = declaration.WithWarning("SetLastError is not supported on virtual methods and will be ignored."); }

            // Skip ABI checks if the function is uncallable
            if (declaration.FunctionAbi is null)
            {
                // Ensure the declaration has an error
                if (!declaration.Diagnostics.Any(d => d.IsError))
                { declaration = declaration.WithError("Function is missing ABI information and as such is not callable."); }

                return declaration;
            }

            // Check for ABI corner cases we don't expect/handle
            // In theory some of the x86 calling conventions work, but they're untested so let's complain if they get used at the code gen level.
            if (declaration.FunctionAbi.CallingConvention != PathogenLlvmCallingConventionKind.C)
            { declaration = declaration.WithWarning($"ABI: LLVM calling convention {declaration.FunctionAbi.CallingConvention} may not be handled correctly."); }
            else if (declaration.FunctionAbi.EffectiveCallingConvention != PathogenLlvmCallingConventionKind.C)
            { declaration = declaration.WithWarning($"ABI: Effective LLVM calling convention {declaration.FunctionAbi.EffectiveCallingConvention} may not be handled correctly."); }

            switch (declaration.FunctionAbi!.AstCallingConvention)
            {
                case PathogenClangCallingConventionKind.C:
                case PathogenClangCallingConventionKind.X86StdCall:
                case PathogenClangCallingConventionKind.X86FastCall:
                case PathogenClangCallingConventionKind.X86ThisCall:
                case PathogenClangCallingConventionKind.Win64:
                    break;
                // Add a warning for anything we don't explicitly recognize
                default:
                    declaration = declaration.WithWarning($"ABI: AST calling convention {declaration.FunctionAbi.AstCallingConvention} may not be handled correctly.");
                    break;
            }

            if (declaration.FunctionAbi!.Flags.HasFlag(PathogenArrangedFunctionFlags.UsesInAlloca))
            { declaration = declaration.WithWarning($"ABI: Function uses inalloca, which might not be handled correctly."); }

            if (declaration.FunctionAbi!.Flags.HasFlag(PathogenArrangedFunctionFlags.HasExtendedParameterInfo))
            { declaration = declaration.WithWarning($"ABI: Function has extended parameter info, which might not be handled correctly."); }

            if (declaration.FunctionAbi!.ReturnInfo.Kind is PathogenArgumentKind.Expand or PathogenArgumentKind.CoerceAndExpand)
            { declaration = declaration.WithWarning($"ABI: Function return value passing kind is {declaration.FunctionAbi.ReturnInfo.Kind}, which might not be handled correctly."); }

            for (int i = 0; i < declaration.FunctionAbi!.ArgumentCount; i++)
            {
                if (declaration.FunctionAbi.Arguments[i].Kind is PathogenArgumentKind.Expand or PathogenArgumentKind.CoerceAndExpand)
                {
                    string parameterDescription;

                    if (declaration.IsInstanceMethod)
                    { parameterDescription = i == 0 ? "this pointer parameter" : $"parameter #{i - 1}"; }
                    else
                    { parameterDescription = $"parameter #{i}"; }

                    declaration = declaration.WithWarning($"ABI: Function {parameterDescription} passing kind is {declaration.FunctionAbi.Arguments[i].Kind}, which might not be handled correctly.");
                }
            }

            return base.TransformFunction(context, declaration);
        }

        protected override TransformationResult TransformParameter(TransformationContext context, TranslatedParameter declaration)
        {
            //TODO: Verify type is compatible
            if (context.ParentDeclaration is not TranslatedFunction)
            { declaration = declaration.WithError("Function parameters are not valid outside of a function context."); }

            //TODO: These default values have already been captured in the trampolines so it's actually too late to remove them
            // Additionally, the user might've added trampolines which *can* handle these defaults so it's hard to say the concept of an incompatible default value really exists anymore
            // We could potentially loop through all of the trampolines and see if any of the input adapters corresponding to this parameter was able to emit this default value and if not
            // emit a warning then. That'd be a decent way to warn that the default value won't be present in the output *and* detect if the generator author handled it themselves.

            // Verify default parameter value is compatible
            switch (declaration.DefaultValue)
            {
                case StringConstant:
                    return declaration with
                    {
                        DefaultValue = null,
                        Diagnostics = declaration.Diagnostics.Add(Severity.Warning, "String constants are not supported as default parameter values.")
                    };
                case UnsupportedConstantExpression unsupportedConstant:
                    return declaration with
                    {
                        DefaultValue = null,
                        Diagnostics = declaration.Diagnostics.Add(Severity.Warning, $"Unsupported constant: {unsupportedConstant.Message}")
                    };
                default:
                {
                    // This can happen if a C# built-in type was replaced with a different type
                    // (Such as a type being replaced with a convienience wrapper.)
                    static bool TypeCanHaveDefaultInCSharp(TranslatedLibrary library, TypeReference type)
                    {
                        switch (type)
                        {
                            case PointerTypeReference:
                            case CSharpBuiltinTypeReference:
                            case FunctionPointerTypeReference:
                                return true;
                            case ExternallyDefinedTypeReference externalType:
                                return externalType == CSharpBuiltinType.String || externalType == CSharpBuiltinType.NativeInt || externalType == CSharpBuiltinType.NativeUnsignedInt;
                            case TranslatedTypeReference translatedType:
                                switch (translatedType.TryResolve(library))
                                {
                                    case TranslatedTypedef typedef:
                                        return TypeCanHaveDefaultInCSharp(library, typedef.UnderlyingType);
                                    case TranslatedEnum:
                                        return true;
                                    // NativeBoolean and NativeChar will become bool/char on the trampoline surface and should not appear in places where trampolines aren't generated since
                                    // they end up using MarshalAs instead.
                                    case NativeBooleanDeclaration:
                                    case NativeCharDeclaration:
                                        return true;
                                    default:
                                        return false;
                                }
                            default:
                                return false;
                        }
                    }

                    if (declaration.DefaultValue is not null && !TypeCanHaveDefaultInCSharp(context.Library, declaration.Type))
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

            // We don't currently provide a way to initialize a virtual type without a constructor
            // https://github.com/MochiLibraries/Biohazrd/issues/31
            if (declaration.VTable is not null
                && !declaration.Members.OfType<TranslatedFunction>().Any(f => f.SpecialFunctionKind is SpecialFunctionKind.Constructor)
                && declaration.Declaration is not CXXRecordDecl { IsAbstract: true })
            { declaration = declaration.WithWarning("Virtual records without a constructor cannot be initialized from C#. See https://github.com/MochiLibraries/Biohazrd/issues/31"); }

            return base.TransformRecord(context, declaration);
        }

        protected override TransformationResult TransformBitField(TransformationContext context, TranslatedBitField declaration)
        {
            static bool CanBeBitFieldType(TranslatedLibrary library, TypeReference type)
            {
                switch (type)
                {
                    // Integral C# built-in types are allowed
                    case CSharpBuiltinTypeReference { Type: { IsIntegral: true } }:
                        return true;
                    // Booleans are supported
                    case CSharpBuiltinTypeReference cSharpBuiltinTypeReference when cSharpBuiltinTypeReference == CSharpBuiltinType.Bool:
                        return true;
                    case TranslatedTypeReference typeReference:
                    {
                        switch (typeReference.TryResolve(library))
                        {
                            // Integral enums are allowed
                            case TranslatedEnum { UnderlyingType: CSharpBuiltinTypeReference { Type: { IsIntegral: true } } }:
                                return true;
                            // For typedefs defer to the underlying type
                            case TranslatedTypedef typedef:
                                return CanBeBitFieldType(library, typedef.UnderlyingType);
                            default:
                                return false;
                        }
                    }
                    default:
                        return false;
                }
            }

            if (CanBeBitFieldType(context.Library, declaration.Type))
            { return base.TransformBitField(context, declaration); }
            else
            { return declaration.WithError($"Bit fields must by typed by an integral C# built-in type or an enum with an integral underlying type. {declaration.Type} is neither."); }
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
