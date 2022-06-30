using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Biohazrd.BoilerplateGenerator;

internal static class RoslynExtensions
{
    public static IncrementalValuesProvider<TResult> OfType<TSource, TResult>(this IncrementalValuesProvider<TSource?> source)
        where TSource : class
        where TResult : class
        => source.Select((x, _) => x as TResult).Where(x => x is not null)!;

    public static IncrementalValuesProvider<TResult> OfType<TResult>(this IncrementalValuesProvider<object?> source)
        where TResult : class
        => OfType<object, TResult>(source);

    public static IncrementalValueProvider<ImmutableArray<T>> CollectAndAppend<T>(this IncrementalValuesProvider<T> a, IncrementalValuesProvider<T> b)
        => a.Collect().Combine(b.Collect())
        .Select(static (pair, _) => pair.Left.AddRange(pair.Right));

    public static bool IsSystemCollectionsImmutable(this INamespaceSymbol? symbol)
    {
        if (symbol?.Name != "Immutable")
        { return false; }

        symbol = symbol.ContainingNamespace;
        if (symbol?.Name != "Collections")
        { return false; }

        symbol = symbol.ContainingNamespace;
        if (symbol?.Name != "System")
        { return false; }

        return symbol.ContainingNamespace?.IsGlobalNamespace ?? false;
    }

    private static bool IsImmutableCollection(this ITypeSymbol? symbol, string name, [NotNullWhen(true)] out ITypeSymbol? elementType)
    {
        elementType = default;

        if (symbol is not INamedTypeSymbol namedSymbol)
        { return false; }

        if (namedSymbol.Arity != 1)
        { return false; }

        if (symbol.Name != name)
        { return false; }

        if (!symbol.ContainingNamespace.IsSystemCollectionsImmutable())
        { return false; }

        Debug.Assert(namedSymbol.TypeArguments.Length == 1);
        Debug.Assert(namedSymbol.TypeArgumentNullableAnnotations.Length == 1);
        elementType = namedSymbol.TypeArguments[0];
        return true;
    }

    public static bool IsImmutableArray(this ITypeSymbol? symbol, [NotNullWhen(true)] out ITypeSymbol? elementType)
        => symbol.IsImmutableCollection("ImmutableArray", out elementType);

    public static bool IsImmutableList(this ITypeSymbol? symbol, [NotNullWhen(true)] out ITypeSymbol? elementType)
        => symbol.IsImmutableCollection("ImmutableList", out elementType);

    public static bool IsBiohazrdType(this ITypeSymbol? symbol, string typeName)
    {
        if (symbol?.Name != typeName)
        { return false; }

        if (symbol.ContainingNamespace?.Name != WellKnown.Biohazrd)
        { return false; }

        return symbol.ContainingNamespace.ContainingNamespace?.IsGlobalNamespace ?? false;
    }

    public static bool IsTranslatedDeclaration(this ITypeSymbol? symbol)
        => symbol.IsBiohazrdType(WellKnown.TranslatedDeclaration);

    public static bool IsTypeReference(this ITypeSymbol? symbol)
        => symbol.IsBiohazrdType(WellKnown.TypeReference);

    public static bool IsBiohazrdInfrastructureType(this ISymbol? symbol, string typeName)
    {
        if (symbol?.Name != typeName)
        { return false; }

        symbol = symbol.ContainingNamespace;
        if (symbol?.Name != "Infrastructure")
        { return false; }

        symbol = symbol.ContainingNamespace;
        if (symbol?.Name != WellKnown.Biohazrd)
        { return false; }

        return symbol.ContainingNamespace?.IsGlobalNamespace ?? false;
    }

    public static bool IsCatchAllMembersPropertyAttribute(this ITypeSymbol? symbol)
        => symbol.IsBiohazrdInfrastructureType(WellKnown.CatchAllMembersPropertyAttribute);

    public static bool IsDoNotGenerateBoilerplateMethodsAttribute(this ITypeSymbol? symbol)
        => symbol.IsBiohazrdInfrastructureType(WellKnown.DoNotGenerateBoilerplateMethodsAttribute);

    public static bool ShouldIgnore(this ITypeSymbol symbol)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass.IsDoNotGenerateBoilerplateMethodsAttribute())
            { return true; }
        }

        return false;
    }
}
