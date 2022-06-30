using Biohazrd.CSharp;
using Microsoft.CodeAnalysis;
using System;
using System.Diagnostics;

namespace Biohazrd.BoilerplateGenerator;

partial class SourceGenerator
{
    private static void TranslatedDeclarationChildrenMethods(SourceProductionContext context, TranslatedDeclarationInfo declaration)
    {
        Debug.Assert(!declaration.IsExternal, "Don't pass external declarations to this method!");

        if (declaration.IsExternal || declaration.ChildDeclarations.Length == 0)
        { return; }

        // If all of our children are inherited we don't need to emit anything because we can use the ones emitted for our parent
        if (declaration.AllChildDeclarationsAreInherited)
        { return; }

        using CSharpCodeWriter writer = new();
        using (writer.Namespace(declaration.Namespace))
        {
            writer.WriteLine($"partial record {declaration.Name}");
            using (writer.Block())
            {
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the TotalMemberCount property
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                if (declaration.ChildDeclarations.Length > 1)
                {
                    writer.EnsureSeparation();
                    writer.WriteLine("public int TotalMemberCount");
                    using (writer.Block())
                    {
                        writer.WriteLine("get");
                        using (writer.Block())
                        {
                            writer.Write("int result = ");

                            bool first = true;
                            int nonNullableSingleValueCount = 0;
                            foreach (PropertyInfo property in declaration.ChildDeclarations)
                            {
                                string lengthProperty;

                                switch (property.Kind)
                                {
                                    case PropertyKind.SingleValue:
                                        nonNullableSingleValueCount++;
                                        continue;
                                    case PropertyKind.SingleNullableValue:
                                        continue;
                                    case PropertyKind.ImmutableArray:
                                        lengthProperty = "Length";
                                        break;
                                    case PropertyKind.ImmutableList:
                                        lengthProperty = "Count";
                                        break;
                                    default:
                                        throw new InvalidOperationException($"Invalid property kind `{property.Kind}`.");
                                }

                                if (first)
                                { first = false; }
                                else
                                { writer.Write(" + "); }

                                writer.Write($"{property.Name}.{lengthProperty}");
                            }

                            if (nonNullableSingleValueCount > 0)
                            {
                                if (first)
                                { first = false; }
                                else
                                { writer.Write(" + "); }

                                writer.Write(nonNullableSingleValueCount);
                            }

                            if (first)
                            { writer.Write('0'); }

                            writer.WriteLine(';');

                            // Add nullable properties when they're present
                            first = true;
                            foreach (PropertyInfo property in declaration.ChildDeclarations)
                            {
                                if (property.Kind != PropertyKind.SingleNullableValue)
                                { continue; }

                                first = false;
                                writer.EnsureSeparation();
                                writer.WriteLine($"if ({property.Name} is not null)");
                                writer.WriteLine("{ result++; }");
                            }

                            if (!first)
                            { writer.WriteLine(); }

                            writer.WriteLine("return result;");
                        }
                    }
                }

                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the GetEnumator method
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                writer.EnsureSeparation();
                writer.Using("System.Collections.Generic"); // IEnumerator<T>
                writer.Using(WellKnown.Biohazrd); // TranslatedDeclaration
                writer.WriteLine("public override IEnumerator<TranslatedDeclaration> GetEnumerator()");

                if (declaration.ChildDeclarations.Length == 1 && declaration.ChildDeclarations[0] is { Kind: PropertyKind.ImmutableList } singleEnumerableChild)
                { writer.WriteLineIndented($"=> {singleEnumerableChild.Name}.GetEnumerator();"); }
                else
                {
                    using (writer.Block())
                    {
                        PropertyInfo? lastProperty = null;
                        foreach (PropertyInfo property in declaration.ChildDeclarations)
                        {
                            switch (property.Kind)
                            {
                                case PropertyKind.SingleValue:
                                    if (lastProperty?.Kind != PropertyKind.SingleValue)
                                    { writer.EnsureSeparation(); }

                                    writer.WriteLine($"yield return {property.Name};");
                                    break;
                                case PropertyKind.SingleNullableValue:
                                    writer.EnsureSeparation();
                                    writer.WriteLine($"if ({property.Name} is not null)");
                                    writer.WriteLine($"{{ yield return {property.Name}; }}");
                                    break;
                                case PropertyKind.ImmutableArray:
                                case PropertyKind.ImmutableList:
                                    writer.EnsureSeparation();
                                    string temporary = property.ElementVariableName;
                                    writer.Using(property.TypeNamespace);
                                    writer.WriteLine($"foreach ({property.TypeName} {temporary} in {property.Name})");
                                    writer.WriteLine($"{{ yield return {temporary}; }}");
                                    break;
                                default:
                                    throw new InvalidOperationException($"Invalid property kind `{property.Kind}`.");
                            }

                            lastProperty = property;
                        }
                    }
                }
            }
        }

        context.AddSource($"{declaration.Name}.gen.cs", writer.Finish());
    }
}
