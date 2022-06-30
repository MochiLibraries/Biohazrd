using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

#pragma warning disable IDE0008 // Some of the intermediate steps of the incremental pipeline are excessively verbose without var.
namespace Biohazrd.BoilerplateGenerator;

// Got a little lazy with diagnostics since this is primarily meant for internal consumption (for now)
// Some things we should definitely detect in the future:
// * Error if multiple properties are catch-all
// * Error if catch-all property is not an array/list of the base type
// * Error if a array/list property has nullable elements
[Generator(LanguageNames.CSharp)]
public sealed partial class SourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Determine the assembly we're targeting
        IncrementalValueProvider<GeneratorTarget> generatorTargetProvider = context.CompilationProvider.Select((c, _) => c.AssemblyName switch
        {
            WellKnown.Biohazrd => GeneratorTarget.BiohazrdAssembly,
            WellKnown.BiohazrdTransformation => GeneratorTarget.BiohazrdTransformationAssembly,
            _ => GeneratorTarget.Other
        });

        // Try to find the main assembly reference
        IncrementalValueProvider<IAssemblySymbol?> mainAssemblyProvider = context.CompilationProvider.Select(static (compilation, _) =>
        {
            foreach (MetadataReference reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol { Name: WellKnown.Biohazrd } mainAssembly)
                { return mainAssembly; }
            }

            return null;
        });

        // Enumerate declarations and type references from the main Biohazrd assembly
        var externalDeclarationsAndTypeReferences = mainAssemblyProvider.Select(static (mainAssembly, cancellationToken) =>
        {
            ExternalSymbolsVisitor visitor = new(mainAssembly, cancellationToken);
            return visitor.GetResults();
        });
        IncrementalValuesProvider<TranslatedDeclarationInfo> externalDeclarations = externalDeclarationsAndTypeReferences.SelectMany((pair, _) => pair.Declarations);
        IncrementalValuesProvider<TypeReferenceInfo> externalTypeReferences = externalDeclarationsAndTypeReferences.SelectMany((pair, _) => pair.TypeReferences);

        // Enumerate declarations and type references from this compilation
        var currentDeclarationsAndTypeReferences = context.SyntaxProvider.CreateSyntaxProvider
        (
            static (node, _) =>
            {
                // Declarations and type references must be records
                if (node is not RecordDeclarationSyntax recordSyntax)
                { return false; }

                // Declarations and type references always have a base type of some kind
                if (recordSyntax.BaseList is null)
                { return false; }

                // Declarations and type references must be public
                if (!recordSyntax.Modifiers.Any(SyntaxKind.PublicKeyword))
                { return false; }

                // If we got this far, the record is a candidate
                return true;
            },
            static object? (context, cancellationToken) =>
            {
                INamedTypeSymbol? type = context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken) as INamedTypeSymbol;
                if (type is null)
                { return null; }

                // Skip explicitly ignored types
                if (type.ShouldIgnore())
                { return null; }

                // Determine if this type is a declaration or type reference
                bool derivesFromTranslatedDeclaration = false;
                bool derivesFromTypeReference = false;
                int distanceFromBase = 0;

                for (ITypeSymbol? baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    distanceFromBase++;

                    if (baseType.IsTranslatedDeclaration())
                    {
                        derivesFromTranslatedDeclaration = true;
                        break;
                    }

                    if (baseType.IsTypeReference())
                    {
                        derivesFromTypeReference = true;
                        break;
                    }
                }

                if (derivesFromTranslatedDeclaration)
                { return new TranslatedDeclarationInfo(type, distanceFromBase, isExternal: false); }
                else if (derivesFromTypeReference)
                { return new TypeReferenceInfo(type, distanceFromBase, isExternal: false); }
                else
                { return null; }
            }
        ).Where(o => o is not null);

        IncrementalValuesProvider<TranslatedDeclarationInfo> currentDeclarations = currentDeclarationsAndTypeReferences.OfType<TranslatedDeclarationInfo>();
        IncrementalValuesProvider<TypeReferenceInfo> currentTypeReferences = currentDeclarationsAndTypeReferences.OfType<TypeReferenceInfo>();

        // Combine the external and current declarations / type references
        IncrementalValueProvider<ImmutableArray<TranslatedDeclarationInfo>> allDeclarations = externalDeclarations.CollectAndAppend(currentDeclarations);
        IncrementalValueProvider<ImmutableArray<TypeReferenceInfo>> allTypeReferences = externalTypeReferences.CollectAndAppend(currentTypeReferences);

        // Misc combined providers
        var allDeclarationsAndTarget = allDeclarations.Combine(generatorTargetProvider);
        var allDeclarationsAndTypeReferencesAndTarget = allDeclarations.Combine(allTypeReferences).Combine(generatorTargetProvider);
        var allTypeReferencesAndTarget = allTypeReferences.Combine(generatorTargetProvider);

#if DEBUG
        //-------------------------------------------------------------------------------------------------------------------------------------------
        // Debug outputs
        //-------------------------------------------------------------------------------------------------------------------------------------------
        context.RegisterSourceOutput(allDeclarations.Combine(allTypeReferences).Combine(generatorTargetProvider).Combine(mainAssemblyProvider), static (context, info) =>
        {
            ImmutableArray<TranslatedDeclarationInfo> allDeclarations = info.Left.Left.Left;
            ImmutableArray<TypeReferenceInfo> allTypeReferences = info.Left.Left.Right;
            GeneratorTarget generatorTarget = info.Left.Right;
            IAssemblySymbol? mainAssembly = info.Right;
            StringBuilder sb = new();

            sb.WriteLine($"// {nameof(GeneratorTarget)} = {generatorTarget}");
            sb.WriteLine($"// Have main assembly? {(mainAssembly is null ? "No" : "Yes")}");

            sb.AppendLine("/* All declarations ===============================================================================================================");

            void WritePropertyInfo(char kind, PropertyInfo property)
            {
                sb.AppendLine($"  {kind}{(property.IsInherited ? "i" : "")} {property.Name} -> " +
                    $"{property.TypeNamespace}.{property.TypeName}{(property.Kind == PropertyKind.SingleNullableValue ? "?" : "")}{(property.TypeIsBiohazrdBase ? "[Base]" : "")} " +
                    $"({(property.Kind == PropertyKind.SingleNullableValue ? PropertyKind.SingleValue : property.Kind)}{(property.IsCatchAllMembersProperty ? ", Catch-all" : "")})");
            }

            foreach (TranslatedDeclarationInfo declaration in allDeclarations.OrderByDescending(d => d.DistanceFromBase))
            {
                sb.AppendLine($"{declaration.Namespace}.{declaration.Name} -> {declaration.ParentName} {(declaration.IsExternal ? "(External)" : "")}");

                foreach (PropertyInfo property in declaration.ChildDeclarations)
                { WritePropertyInfo('D', property); }

                foreach (PropertyInfo property in declaration.ChildTypeReferences)
                { WritePropertyInfo('T', property); }
            }
            sb.AppendLine("*/");

            sb.AppendLine("/* All type references ============================================================================================================");
            foreach (TypeReferenceInfo typeReference in allTypeReferences.OrderByDescending(t => t.DistanceFromBase))
            {
                sb.AppendLine($"{typeReference.Namespace}.{typeReference.Name} -> {typeReference.ParentName} {(typeReference.IsExternal ? "(External)" : "")}");

                foreach (PropertyInfo property in typeReference.ChildTypeReferences)
                { WritePropertyInfo('T', property); }
            }
            sb.AppendLine("*/");

            context.AddSource("DebugOutput.gen.cs", sb.ToString());
        });
#endif

        //-------------------------------------------------------------------------------------------------------------------------------------------
        // Biohazrd main assembly outputs
        //-------------------------------------------------------------------------------------------------------------------------------------------
        context.RegisterSourceOutput(allDeclarationsAndTarget, static (context, tuple) => GenerateDeclarationVisitorMethods(context, tuple.Left, tuple.Right));

        //-------------------------------------------------------------------------------------------------------------------------------------------
        // Biohazrd.Transformation outputs
        //-------------------------------------------------------------------------------------------------------------------------------------------
        context.RegisterSourceOutput(allDeclarationsAndTarget, static (context, tuple) => GenerateRawTransformationBaseMethods(context, tuple.Left, tuple.Right));
        context.RegisterSourceOutput(allDeclarationsAndTarget, static (context, tuple) => GenerateTransformationBaseMethods(context, tuple.Left, tuple.Right));

        context.RegisterSourceOutput(allDeclarationsAndTypeReferencesAndTarget, static (context, tuple) => GenerateRawTypeTransformationBaseMethods(context, tuple.Left.Left, tuple.Left.Right, tuple.Right));
        context.RegisterSourceOutput(allTypeReferencesAndTarget, static (context, tuple) => GenerateTypeTransformationBaseMethods(context, tuple.Left, tuple.Right));

        context.RegisterSourceOutput(allDeclarationsAndTarget, static (context, tuple) => GenerateSimpleTransformation(context, tuple.Left, tuple.Right));

        //-------------------------------------------------------------------------------------------------------------------------------------------
        // General outputs
        //-------------------------------------------------------------------------------------------------------------------------------------------
        context.RegisterSourceOutput(currentDeclarations, TranslatedDeclarationChildrenMethods);
    }
}
