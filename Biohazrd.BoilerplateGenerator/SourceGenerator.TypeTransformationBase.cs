using Biohazrd.CSharp;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Linq;

namespace Biohazrd.BoilerplateGenerator;

partial class SourceGenerator
{
    private static void GenerateTypeTransformationBaseMethods(SourceProductionContext context, ImmutableArray<TypeReferenceInfo> allTypeReferences, GeneratorTarget generatorTarget)
    {
        if (generatorTarget != GeneratorTarget.BiohazrdTransformationAssembly)
        { return; }

        using CSharpCodeWriter writer = new();
        using (writer.Namespace(WellKnown.BiohazrdTransformation))
        {
            writer.WriteLine($"partial class {WellKnown.TypeTransformationBase}");
            using (writer.Block())
            {
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the TransformType method
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                writer.WriteLine("protected override TypeTransformationResult TransformType(TypeTransformationContext context, TypeReference type)");
                using (writer.Indent())
                {
                    writer.WriteLine("=> type switch");
                    using (writer.BlockWithSemiColon())
                    {
                        foreach (TypeReferenceInfo typeReference in allTypeReferences.OrderByDescending(d => d.DistanceFromBase))
                        {
                            writer.Using(typeReference.Namespace);
                            string temporary = typeReference.TemporaryVariableName;
                            writer.WriteLine($"{typeReference.Name} {temporary} => {typeReference.TransformMethodName}(context, {temporary}),");
                        }

                        // Fallback
                        writer.WriteLine($"TypeReference => {WellKnown.TransformUnknownTypeReference}(context, type)");
                    }
                }

                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the individual type reference transformation methods
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // All the individual transform methods are short, only separate here and let them squish together
                writer.EnsureSeparation();

                foreach (TypeReferenceInfo typeReference in allTypeReferences.OrderByDescending(d => d.DistanceFromBase))
                {
                    writer.Using(typeReference.Namespace);
                    writer.WriteLine($"protected virtual TypeTransformationResult {typeReference.TransformMethodName}(TypeTransformationContext context, {typeReference.Name} type)");
                    writer.WriteLineIndented($"=> {TypeReferenceInfo.MakeTransformMethodName(typeReference.ParentName)}(context, type);");
                }
            }
        }

        context.AddSource($"{WellKnown.TypeTransformationBase}.gen.cs", writer.Finish());
    }
}
