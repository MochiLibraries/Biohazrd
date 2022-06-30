using Biohazrd.CSharp;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Linq;

namespace Biohazrd.BoilerplateGenerator;

partial class SourceGenerator
{
    private static void GenerateTransformationBaseMethods(SourceProductionContext context, ImmutableArray<TranslatedDeclarationInfo> allDeclarations, GeneratorTarget generatorTarget)
    {
        if (generatorTarget != GeneratorTarget.BiohazrdTransformationAssembly)
        { return; }

        using CSharpCodeWriter writer = new();
        using (writer.Namespace(WellKnown.BiohazrdTransformation))
        {
            writer.WriteLine($"partial class {WellKnown.TransformationBase}");
            using (writer.Block())
            {
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the Transform method
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                writer.WriteLine("protected override TransformationResult Transform(TransformationContext context, TranslatedDeclaration declaration)");
                using (writer.Indent())
                {
                    writer.WriteLine("=> declaration switch");
                    using (writer.BlockWithSemiColon())
                    {
                        foreach (TranslatedDeclarationInfo declaration in allDeclarations.OrderByDescending(d => d.DistanceFromBase))
                        {
                            writer.Using(declaration.Namespace);
                            string temporary = declaration.TemporaryVariableName;
                            writer.WriteLine($"{declaration.Name} {temporary} => {declaration.TransformMethodName}(context, {temporary}),");
                        }

                        // Fallback
                        writer.WriteLine($"{WellKnown.TranslatedDeclaration} => {WellKnown.TransformUnknownDeclarationType}(context, declaration)");
                    }
                }

                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the individual TransformXyz methods
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // All the individual transform methods are short, only separate here and let them squish together
                writer.EnsureSeparation();

                foreach (TranslatedDeclarationInfo declaration in allDeclarations.OrderByDescending(d => d.DistanceFromBase))
                {
                    writer.Using(declaration.Namespace);
                    writer.WriteLine($"protected virtual TransformationResult {declaration.TransformMethodName}(TransformationContext context, {declaration.Name} declaration)");
                    writer.WriteLineIndented($"=> {TranslatedDeclarationInfo.MakeTransformMethodName(declaration.ParentName)}(context, declaration);");
                }
            }
        }

        context.AddSource($"{WellKnown.TransformationBase}.gen.cs", writer.Finish());
    }
}
