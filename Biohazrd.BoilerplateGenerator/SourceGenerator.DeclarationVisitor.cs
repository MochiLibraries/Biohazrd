using Biohazrd.CSharp;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Linq;

namespace Biohazrd.BoilerplateGenerator;

partial class SourceGenerator
{
    private static void GenerateDeclarationVisitorMethods(SourceProductionContext context, ImmutableArray<TranslatedDeclarationInfo> allDeclarations, GeneratorTarget generatorTarget)
    {
        if (generatorTarget != GeneratorTarget.BiohazrdAssembly)
        { return; }

        using CSharpCodeWriter writer = new();
        using (writer.Namespace(WellKnown.Biohazrd))
        {
            writer.WriteLine($"partial class {WellKnown.DeclarationVisitor}");
            using (writer.Block())
            {
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the main visit method
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                writer.Using("Biohazrd"); // VisitorContext, TranslatedDeclaration
                writer.EnsureSeparation();
                writer.WriteLine($"protected virtual void Visit(VisitorContext context, TranslatedDeclaration declaration)");
                using (writer.Block())
                {
                    writer.WriteLine("switch (declaration)");
                    using (writer.Block())
                    {
                        foreach (TranslatedDeclarationInfo declaration in allDeclarations.OrderByDescending(d => d.DistanceFromBase))
                        {
                            writer.Using(declaration.Namespace);
                            writer.WriteLine($"case {declaration.Name} {declaration.TemporaryVariableName}:");
                            writer.WriteLineIndented($"{declaration.VisitMethodName}(context, {declaration.TemporaryVariableName});");
                            writer.WriteLineIndented("break;");
                        }

                        // Fallback
                        writer.WriteLine("default:");
                        writer.WriteLineIndented($"{WellKnown.VisitUnknownDeclarationType}(context, declaration);");
                        writer.WriteLineIndented("break;");
                    }
                }

                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Write out the default implementations for the visit methods
                //---------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // All the visit methods are short, only separate here and let them squish together
                writer.EnsureSeparation();

                writer.Using("Biohazrd"); // VisitorContext
                foreach (TranslatedDeclarationInfo declaration in allDeclarations.OrderBy(d => d.DistanceFromBase))
                {
                    writer.Using(declaration.Namespace);
                    writer.WriteLine($"protected virtual void {declaration.VisitMethodName}(VisitorContext context, {declaration.Name} declaration)");
                    writer.WriteLineIndented($"=> {TranslatedDeclarationInfo.MakeVisitMethodName(declaration.ParentName)}(context, declaration);");
                }
            }
        }

        context.AddSource($"{WellKnown.DeclarationVisitor}.gen.cs", writer.Finish());
    }
}
