using Biohazrd.CSharp;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Biohazrd.BoilerplateGenerator;

partial class SourceGenerator
{
    private static void GenerateRawTypeTransformationBaseMethods
    (
        SourceProductionContext context,
        ImmutableArray<TranslatedDeclarationInfo> allDeclarations,
        ImmutableArray<TypeReferenceInfo> allTypeReferences,
        GeneratorTarget generatorTarget
    )
    {
        if (generatorTarget != GeneratorTarget.BiohazrdTransformationAssembly)
        { return; }

        using CSharpCodeWriter writer = new();
        using (writer.Namespace(WellKnown.BiohazrdTransformation))
        {
            writer.WriteLine($"partial class {WellKnown.RawTypeTransformationBase}");
            using (writer.Block())
            {
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the Transform method
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                writer.EnsureSeparation();
                writer.WriteLine("protected sealed override TransformationResult Transform(TransformationContext context, TranslatedDeclaration declaration)");
                using (writer.Indent())
                {
                    writer.WriteLine("=> declaration switch");
                    using (writer.BlockWithSemiColon())
                    {
                        // Special case: Handle custom declarations
                        writer.Using(WellKnown.BiohazrdTransformationInfrastructure); // ICustomTranslatedDeclaration
                        writer.WriteLine("ICustomTranslatedDeclaration customDeclaration => customDeclaration.TransformTypeChildren(this, context.Add(declaration)),");

                        // Handle each declaration which has type references
                        foreach (TranslatedDeclarationInfo declaration in allDeclarations.OrderByDescending(d => d.DistanceFromBase))
                        {
                            if (declaration.ChildTypeReferences.Length == 0 || declaration.AllChildTypeReferencesAreInherited)
                            { continue; }

                            // To save on the extra context allocation (mainly in the case of TranslatedConstant) we pre-check when there's a single nullable reference
                            // (Might reivist this later to make it work when there's multiple, mainly doing this becuase it was done in the previously manually-authored version of this method.)
                            PropertyInfo? singleNullableTypeReference = null;
                            foreach (PropertyInfo property in declaration.ChildTypeReferences)
                            {
                                if (property.Kind == PropertyKind.SingleNullableValue)
                                {
                                    if (singleNullableTypeReference is not null)
                                    {
                                        singleNullableTypeReference = null;
                                        break;
                                    }

                                    singleNullableTypeReference = property;
                                }
                            }

                            // Write out the case
                            writer.Using(declaration.Namespace);
                            writer.Write($"{declaration.Name} ");

                            if (singleNullableTypeReference is not null)
                            { writer.Write($"{{ {singleNullableTypeReference.Name}: not null }} "); }

                            string temporary = declaration.TemporaryVariableName;
                            writer.WriteLine($"{temporary} => {declaration.TransformTypeReferencesMethodName}(context.Add(declaration), {temporary}),");
                        }

                        // Default case: Declaration has no type references, nothing to do
                        writer.WriteLine("TranslatedDeclaration => declaration");
                    }
                }

                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the TransformTypeChildren method
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                writer.EnsureSeparation();
                writer.WriteLine("protected virtual TypeTransformationResult TransformTypeChildren(TypeTransformationContext context, TypeReference type)");
                using (writer.Indent())
                {
                    writer.WriteLine("=> type switch");
                    using (writer.BlockWithSemiColon())
                    {
                        // Special case: Handle custom type references
                        writer.Using(WellKnown.BiohazrdTransformationInfrastructure); // ICustomTypeReference
                        writer.WriteLine("ICustomTypeReference customTypeReference => customTypeReference.TransformChildren(this, context.Add(type)),");

                        // Handle each type reference which has nested type reference children
                        foreach (TypeReferenceInfo typeReference in allTypeReferences.OrderByDescending(d => d.DistanceFromBase))
                        {
                            if (typeReference.ChildTypeReferences.Length == 0)
                            { continue; }

                            writer.Using(typeReference.Namespace);
                            string temporary = typeReference.TemporaryVariableName;
                            writer.WriteLine($"{typeReference.Name} {temporary} => {typeReference.TransformChildrenMethodName}(context.Add(type), {temporary}),");
                        }

                        // Default case: Type references with no type reference children
                        writer.WriteLine("TypeReference => type");
                    }
                }

                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Common helper for TransformXyzTypeReferenceChildren/TransformXyzTypeReferences methods
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                void WriteOutTypeReferencePropertiesTransformations(ImmutableArray<PropertyInfo> properties, string target)
                {
                    writer.EnsureSeparation();
                    writer.Using(WellKnown.BiohazrdTransformationInfrastructure); // DiagnosticAccumulator, SingleTypeTransformHelper, TypeArrayTransformHelper
                    writer.WriteLine("DiagnosticAccumulator diagnostics = new();");

                    // Write out transform helpers
                    foreach (PropertyInfo property in properties)
                    {
                        // Do some quick and dirty diagnostic emit for situations we do not support
                        // (We should just emit proper diagnostics here but we don't reatain the information necessary to properly do so.)
                        switch (property.Kind)
                        {
                            case PropertyKind.SingleValue:
                            case PropertyKind.SingleNullableValue:
                            case PropertyKind.ImmutableArray:
                                break;
                            default:
                                writer.WriteLineLeftAdjusted($"#error Type reference property '{property.Name}' is a {property.Kind} property, which is currently not supported by the generator.");
                                continue;
                        }

                        // It makes a lot less sense to be able to store specific kinds of type references so this is not worth supporting
                        // Additionally we do not have generic transformation helpers for specific type references.
                        if (!property.TypeIsBiohazrdBase)
                        {
                            writer.WriteLineLeftAdjusted($"#error Type reference property '{property.Name}' is typed as {property.TypeName}, which is currently not supported by the generator.");
                            continue;
                        }

                        switch (property.Kind)
                        {
                            case PropertyKind.SingleValue:
                            case PropertyKind.SingleNullableValue:
                                writer.Write("SingleTypeTransformHelper");
                                break;
                            case PropertyKind.ImmutableArray:
                                writer.Write("TypeArrayTransformHelper");
                                break;
                            default:
                                throw new InvalidOperationException("Invalid or unsupported property kind!");
                        }

                        writer.WriteLine($" new{property.Name} = new({target}.{property.Name}, ref diagnostics);");
                    }

                    // Transform each property
                    foreach (PropertyInfo property in properties)
                    {
                        writer.EnsureSeparation();
                        writer.WriteLine($"// Transform {property.Name}");

                        void WriteSingleValue()
                            => writer.Write($"new{property.Name}.SetValue(TransformTypeRecursively(context, {target}.{property.Name}));");

                        switch (property.Kind)
                        {
                            case PropertyKind.SingleValue:
                            {
                                WriteSingleValue();
                                writer.WriteLine();
                                break;
                            }
                            case PropertyKind.SingleNullableValue:
                            {
                                writer.WriteLine($"if ({target}.{property.Name} is not null)");
                                writer.Write("{ ");
                                WriteSingleValue();
                                writer.WriteLine(" }");
                                break;
                            }
                            case PropertyKind.ImmutableArray:
                            {
                                string temporary = property.ElementVariableName;
                                writer.WriteLine($"foreach (TypeReference {temporary} in {target}.{property.Name})");
                                writer.WriteLine($"{{ new{property.Name}.Add(TransformTypeRecursively(context, {temporary})); }}");
                                writer.WriteLine();
                                // Unlike declaration transformations, type transformations aren't permitted to change the size of the array
                                // There is validation of this in Finish, so we call it explicitly to ensure that validation always happens
                                writer.WriteLine($"new{property.Name}.Finish();");
                                break;
                            }
                            default:
                                throw new InvalidOperationException("Unexpected property kind.");
                        }
                    }
                }

                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the TransformXyzTypeReferenceChildren methods
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                foreach (TypeReferenceInfo typeReference in allTypeReferences.OrderByDescending(d => d.DistanceFromBase))
                {
                    if (typeReference.ChildTypeReferences.Length == 0)
                    { continue; }

                    writer.EnsureSeparation();
                    writer.WriteLine($"private TypeTransformationResult {typeReference.TransformChildrenMethodName}(TypeTransformationContext context, {typeReference.Name} type)");
                    using (writer.Block())
                    {
                        // Write out the type transformations
                        WriteOutTypeReferencePropertiesTransformations(typeReference.ChildTypeReferences, "type");

                        // Create the result
                        writer.EnsureSeparation();
                        writer.WriteLine("// Create the result");
                        writer.WriteLine("TypeTransformationResult result;");
                        writer.WriteLine();
                        writer.Write("if (");

                        bool first = true;
                        foreach (PropertyInfo property in typeReference.ChildTypeReferences)
                        {
                            if (first)
                            { first = false; }
                            else
                            { writer.Write(" || "); }
                            writer.Write($"new{property.Name}.WasChanged");
                        }

                        writer.WriteLine(")");
                        using (writer.Block())
                        {
                            writer.WriteLine("result = type with");
                            using (writer.BlockWithSemiColon())
                            {
                                foreach (PropertyInfo property in typeReference.ChildTypeReferences)
                                {
                                    writer.Write($"{property.Name} = new{property.Name}.");
                                    // We don't emit a null check here for non-nullable properties because while we do support type references which are null,
                                    // we do not support removing type references via type transformation. (IE: TypeTransformationResult doesn't have a concept of removing a type reference.)
                                    // In theory you could force it by returning default(TypeTransformationResult), this is defended against in the transformation helpers.
                                    property.WriteTransformationResultAccess(writer, withNullCheck: false);
                                    writer.WriteLine(',');
                                }
                            }
                        }
                        writer.WriteLine("else");
                        writer.WriteLine("{ result = type; }");
                        writer.WriteLine();
                        writer.WriteLine("// Add any diagnostics to the result and return");
                        writer.WriteLine("result = result.AddDiagnostics(diagnostics.MoveToImmutable());");
                        writer.WriteLine("return result;");
                    }
                }

                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the TransformXyzTypeReferences methods
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                foreach (TranslatedDeclarationInfo declaration in allDeclarations.OrderByDescending(d => d.DistanceFromBase))
                {
                    if (declaration.ChildTypeReferences.Length == 0 || declaration.AllChildTypeReferencesAreInherited)
                    { continue; }

                    writer.EnsureSeparation();
                    writer.WriteLine($"private TransformationResult {declaration.TransformTypeReferencesMethodName}(TransformationContext context, {declaration.Name} declaration)");
                    using (writer.Block())
                    {
                        // Write out the type transformations
                        WriteOutTypeReferencePropertiesTransformations(declaration.ChildTypeReferences, "declaration");

                        // Apply the result
                        writer.EnsureSeparation();
                        writer.WriteLine("// Apply the transformed type references");
                        writer.Write("if (diagnostics.HasDiagnostics");
                        foreach (PropertyInfo property in declaration.ChildTypeReferences)
                        { writer.Write($" || new{property.Name}.WasChanged"); }
                        writer.WriteLine(')');
                        using (writer.Block())
                        {
                            writer.WriteLine("return declaration with");
                            using (writer.BlockWithSemiColon())
                            {
                                foreach (PropertyInfo property in declaration.ChildTypeReferences)
                                {
                                    writer.Write($"{property.Name} = new{property.Name}.");
                                    property.WriteTransformationResultAccess(writer, withNullCheck: false); // See note above on why null check is skipped
                                    writer.WriteLine(',');
                                }

                                writer.WriteLine("Diagnostics = declaration.Diagnostics.AddRange(diagnostics.MoveToImmutable())");
                            }
                        }
                        writer.WriteLine("else");
                        writer.WriteLine("{ return declaration; }");
                    }
                }
            }
        }

        context.AddSource($"{WellKnown.RawTypeTransformationBase}.gen.cs", writer.Finish());
    }
}
