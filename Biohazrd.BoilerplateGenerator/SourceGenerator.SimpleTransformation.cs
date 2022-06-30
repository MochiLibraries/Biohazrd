using Biohazrd.CSharp;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Biohazrd.BoilerplateGenerator;

partial class SourceGenerator
{
    private static void GenerateSimpleTransformation(SourceProductionContext context, ImmutableArray<TranslatedDeclarationInfo> allDeclarations, GeneratorTarget generatorTarget)
    {
        if (generatorTarget != GeneratorTarget.BiohazrdTransformationAssembly)
        { return; }

        using CSharpCodeWriter writer = new();
        using (writer.Namespace(WellKnown.BiohazrdTransformationCommon))
        {
            writer.WriteLine($"partial record {WellKnown.SimpleTransformation}");
            using (writer.Block())
            {
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out delegates for every declaration type
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                foreach (TranslatedDeclarationInfo declaration in allDeclarations)
                {
                    writer.Using(declaration.Namespace);
                    writer.WriteLine($"public TransformationMethod<{declaration.Name}>? {declaration.TransformMethodName} {{ get; init; }}");
                }

                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the TransformXyz methods in the internal transformation
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                writer.EnsureSeparation();
                writer.WriteLine("partial class Transformation");
                using (writer.Block())
                {
                    foreach (TranslatedDeclarationInfo declaration in allDeclarations)
                    {
                        string transformMethod = declaration.TransformMethodName;
                        writer.WriteLine($"protected sealed override TransformationResult {transformMethod}(TransformationContext context, {declaration.Name} declaration)");
                        writer.WriteLineIndented($"=> Parent.{transformMethod} is not null ? Parent.{transformMethod}(context, declaration) : base.{transformMethod}(context, declaration);");
                    }
                }
            }
        }

        context.AddSource($"{WellKnown.SimpleTransformation}.gen.cs", writer.Finish());
    }
}
