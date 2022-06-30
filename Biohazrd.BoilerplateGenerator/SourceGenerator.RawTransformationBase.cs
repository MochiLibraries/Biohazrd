using Biohazrd.CSharp;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Biohazrd.BoilerplateGenerator;

partial class SourceGenerator
{
    private static void GenerateRawTransformationBaseMethods(SourceProductionContext context, ImmutableArray<TranslatedDeclarationInfo> allDeclarations, GeneratorTarget generatorTarget)
    {
        if (generatorTarget != GeneratorTarget.BiohazrdTransformationAssembly)
        { return; }

        using CSharpCodeWriter writer = new();
        using (writer.Namespace(WellKnown.BiohazrdTransformation))
        {
            writer.WriteLine($"partial class {WellKnown.RawTransformationBase}");
            using (writer.Block())
            {
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the TransformChildren method
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                writer.Using(WellKnown.BiohazrdTransformation); // TransformationResult, TransformationContext
                writer.Using(WellKnown.Biohazrd); // TranslatedDeclaration
                writer.EnsureSeparation();
                writer.WriteLine("private TransformationResult TransformChildren(TransformationContext context, TranslatedDeclaration declaration)");
                using (writer.Indent())
                {
                    writer.WriteLine($"=> declaration switch");
                    using (writer.BlockWithSemiColon())
                    {
                        // Special case: Handle custom declarations
                        writer.Using(WellKnown.BiohazrdTransformationInfrastructure); // ICustomTranslatedDeclaration
                        writer.WriteLine("ICustomTranslatedDeclaration customDeclaration => customDeclaration.TransformChildren(this, context.Add(declaration)),");

                        foreach (TranslatedDeclarationInfo declaration in allDeclarations.OrderByDescending(d => d.DistanceFromBase))
                        {
                            if (declaration.ChildDeclarations.Length == 0 || declaration.AllChildDeclarationsAreInherited)
                            { continue; }

                            string temporary = declaration.TemporaryVariableName;
                            writer.Using(declaration.Namespace);
                            writer.WriteLine($"{declaration.Name} {temporary} => {declaration.TransformChildrenMethodName}(context.Add(declaration), {temporary}),");
                        }

                        // Default case: Do nothing for declarations with no children
                        writer.WriteLine("// In the default case, the declaration has no children:");
                        writer.WriteLine("TranslatedDeclaration => declaration");
                    }
                }

                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the individual TransformXyzChildren methods
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Easiest solution would be to just have propertyinfos propagate to children.
                writer.Using(WellKnown.BiohazrdTransformation); // TransformationResult, TransformationContext
                writer.Using(WellKnown.BiohazrdTransformationInfrastructure); // ArrayTransformHelper<T>, ListTransformHelper, ListTransformHelper<T>, SingleTransformHelper<T>
                foreach (TranslatedDeclarationInfo declaration in allDeclarations.OrderByDescending(d => d.DistanceFromBase))
                {
                    if (declaration.ChildDeclarations.Length == 0 || declaration.AllChildDeclarationsAreInherited)
                    { continue; }

                    writer.EnsureSeparation();
                    writer.Using(declaration.Namespace);
                    writer.WriteLine($"private TransformationResult {declaration.TransformChildrenMethodName}(TransformationContext context, {declaration.Name} declaration)");
                    using (writer.Block())
                    {
                        // Write out helpers for detecting changes
                        foreach (PropertyInfo property in declaration.ChildDeclarations)
                        {
                            writer.Using(property.TypeNamespace);

                            switch (property.Kind)
                            {
                                case PropertyKind.ImmutableArray:
                                    writer.WriteLine($"ArrayTransformHelper<{property.TypeName}> new{property.Name} = new(declaration.{property.Name});");
                                    break;
                                case PropertyKind.ImmutableList:
                                    writer.Write("using ListTransformHelper");

                                    // If the type isn't TranslatedDeclaration, use ListTransformHelper<T> otherwise use ListTransformHelper
                                    if (!property.TypeIsBiohazrdBase)
                                    { writer.Write($"<{property.TypeName}>"); }

                                    writer.WriteLine($" new{property.Name} = new(declaration.{property.Name});");
                                    break;
                                case PropertyKind.SingleValue:
                                case PropertyKind.SingleNullableValue:
                                    writer.WriteLine($"SingleTransformHelper<{property.TypeName}> new{property.Name} = new(declaration.{property.Name});");
                                    break;
                                default:
                                    throw new InvalidOperationException($"Invalid property kind '{property.Kind}'");
                            }
                        }

                        // Determine if we have a catch-all members property
                        PropertyInfo? catchAllMembersProperty = declaration.ChildDeclarations.FirstOrDefault(d => d.IsCatchAllMembersProperty);

                        // Write out recursive transformations
                        foreach (PropertyInfo property in declaration.ChildDeclarations)
                        {
                            writer.EnsureSeparation();
                            writer.WriteLine($"// Transform {property.Name}");
                            switch (property.Kind)
                            {
                                case PropertyKind.ImmutableArray:
                                case PropertyKind.ImmutableList:
                                {
                                    string elementVariableName = property.ElementVariableName;
                                    writer.WriteLine($"foreach ({property.TypeName} {elementVariableName} in declaration.{property.Name})");

                                    void WriteLoopBody()
                                        => writer.Write($"new{property.Name}.Add(TransformRecursively(context, {elementVariableName}));");

                                    if (catchAllMembersProperty is not null || property.TypeIsBiohazrdBase)
                                    {
                                        writer.Write("{ ");
                                        WriteLoopBody();
                                        writer.WriteLine(" }");
                                    }
                                    else
                                    {
                                        using (writer.Block())
                                        {
                                            WriteLoopBody();
                                            writer.WriteLine();

                                            // If we don't have somewhere to put other declarations, fail early
                                            writer.WriteLine();
                                            writer.WriteLine($"if (new{property.Name}.HasOtherDeclarations)");
                                            writer.Using("System"); // InvalidOperationException
                                            writer.WriteLine($"{{ throw new InvalidOperationException(\"Tried to transform a {property.Name} element to something other than {property.TypeName}, " +
                                                "which is not valid in the current context.\"); }");
                                        }
                                    }
                                    break;
                                }
                                case PropertyKind.SingleValue:
                                case PropertyKind.SingleNullableValue:
                                {
                                    void WriteAssignment()
                                        => writer.Write($"new{property.Name}.SetValue(TransformRecursively(context, declaration.{property.Name}));");

                                    bool wantsExtrasCheck = catchAllMembersProperty is null;

                                    void WriteExtrasCheck()
                                    {
                                        writer.WriteLine();
                                        writer.WriteLine($"if (new{property.Name}.HasExtraValues)");
                                        writer.Using("System"); // InvalidOperationException

                                        if (property.TypeIsBiohazrdBase)
                                        {
                                            writer.WriteLine($"{{ throw new InvalidOperationException(\"Tried to transform {property.Name} into multiple declarations, " +
                                                "which is not possible in the current context..\"); }");
                                        }
                                        else
                                        {
                                            writer.WriteLine($"{{ throw new InvalidOperationException(\"Tried to transform a {property.Name} into multiple declarations and/or something other than {property.TypeName}, " +
                                                "which is not valid in the current context.\"); }");
                                        }
                                    }

                                    if (property.Kind == PropertyKind.SingleNullableValue)
                                    {
                                        writer.Write($"if (declaration.{property.Name} is not null)");
                                        if (wantsExtrasCheck)
                                        {
                                            using (writer.Block())
                                            {
                                                WriteAssignment();
                                                writer.WriteLine();
                                                if (wantsExtrasCheck)
                                                { WriteExtrasCheck(); }
                                            }
                                        }
                                        else
                                        {
                                            writer.Write("{ ");
                                            WriteAssignment();
                                            writer.WriteLine(" }");
                                        }
                                    }
                                    else
                                    {
                                        WriteAssignment();
                                        writer.WriteLine();
                                        if (wantsExtrasCheck)
                                        { WriteExtrasCheck(); }
                                    }
                                    break;
                                }
                                default:
                                    throw new InvalidOperationException($"Invalid property kind '{property.Kind}'");
                            }
                        }

                        // Write out mutation
                        writer.EnsureSeparation();
                        writer.WriteLine("// Mutate the declaration if any children changed");
                        writer.Write("if (");
                        bool first = true;
                        foreach (PropertyInfo property in declaration.ChildDeclarations)
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
                            // Handle adding extra outputs to the catch-all members property if we have one
                            if (catchAllMembersProperty is not null)
                            {
                                writer.WriteLine($"// If any fields were replaced with declaration(s) which are not compatible with said field, move them to {catchAllMembersProperty.Name}");
                                writer.Using("Biohazrd.Transformation"); // TransformationResult
                                writer.WriteLine($"TransformationResult extra{catchAllMembersProperty.Name} = new();");

                                foreach (PropertyInfo property in declaration.ChildDeclarations)
                                {
                                    if (property.IsCatchAllMembersProperty)
                                    { continue; }

                                    // Skip properties which allow multiple elements and cannot have extras
                                    if (property.TypeIsBiohazrdBase && property.Kind is PropertyKind.ImmutableArray or PropertyKind.ImmutableList)
                                    {
                                        // Emit sanity check that the other declarations list of ArrayTransformHelper<T> is empty
                                        // (We have to do this because there is no non-generic ArrayTransformHelper)
                                        if (property.Kind == PropertyKind.ImmutableArray)
                                        {
                                            writer.EnsureSeparation();
                                            writer.Using("System.Diagnostics"); // Debug
                                            writer.WriteLineLeftAdjusted($"#warning Property '{property.Kind}' has sub-optimal transformation logic due to yet-to-be-implemented ArrayTransformHelper.");
                                            writer.WriteLine($"Debug.Assert(!new{property.Name}.HasOtherDeclarations, \"An ArrayTransformHelper of {WellKnown.TranslatedDeclaration} should not have other declarations!\");");
                                        }

                                        continue;
                                    }

                                    writer.EnsureSeparation();
                                    string hasExtras;
                                    string getExtras;
                                    switch (property.Kind)
                                    {
                                        case PropertyKind.SingleValue:
                                        case PropertyKind.SingleNullableValue:
                                            hasExtras = "HasExtraValues";
                                            getExtras = "ExtraValues";
                                            break;
                                        case PropertyKind.ImmutableArray:
                                        case PropertyKind.ImmutableList:
                                            hasExtras = "HasOtherDeclarations";
                                            getExtras = "GetOtherDeclarations()";
                                            break;
                                        default:
                                            throw new InvalidOperationException($"Unknown property kind `{property.Kind}`.");
                                    }

                                    writer.WriteLine($"if (new{property.Name}.{hasExtras})");
                                    writer.WriteLine($"{{ extra{catchAllMembersProperty.Name}.AddRange(new{property.Name}.{getExtras}); }}");
                                }

                                // Add the extras to the catch-all property
                                writer.EnsureSeparation();
                                writer.WriteLine($"if (extra{catchAllMembersProperty.Name}.Count > 0)");
                                writer.WriteLine($"{{ new{catchAllMembersProperty.Name}.Add(extra{catchAllMembersProperty.Name}); }}");
                            }

                            // Mutate the declaration
                            writer.EnsureSeparation();
                            writer.WriteLine("// Mutate the declaration");
                            writer.WriteLine("return declaration with");
                            using (writer.BlockWithSemiColon())
                            {
                                foreach (PropertyInfo property in declaration.ChildDeclarations)
                                {
                                    writer.Write($"{property.Name} = new{property.Name}.");
                                    property.WriteTransformationResultAccess(writer);
                                    writer.WriteLine(',');
                                }
                            }
                        }
                        writer.WriteLine("else");
                        writer.WriteLine("{ return declaration; }");
                    }
                }
            }
        }

        context.AddSource($"{WellKnown.RawTransformationBase}.gen.cs", writer.Finish());
    }
}
