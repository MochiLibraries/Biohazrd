using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Biohazrd.BoilerplateGenerator;

internal sealed record TypeReferenceInfo : TypeInfoBase
{
    public ImmutableArray<PropertyInfo> ChildTypeReferences { get; }
    public bool AllChildTypeReferencesAreInherited { get; }

    public TypeReferenceInfo(ITypeSymbol symbol, int distanceFromBase, bool isExternal)
        : base(symbol, distanceFromBase, isExternal)
    {
        ImmutableArray<PropertyInfo>.Builder childTypeReferences = ImmutableArray.CreateBuilder<PropertyInfo>();

        bool isInherited = false;
        for (ITypeSymbol? sourceSymbol = symbol; sourceSymbol != null && !sourceSymbol.IsTypeReference(); sourceSymbol = sourceSymbol.BaseType)
        {
            foreach (ISymbol member in sourceSymbol.GetMembers())
            {
                if (member is not IPropertySymbol property)
                { continue; }

                PropertyInfo.TryCreate(property, declarationProperties: null, childTypeReferences, isInherited);
            }

            isInherited = true;
        }

        ChildTypeReferences = childTypeReferences.MoveToImmutableSafe();
        AllChildTypeReferencesAreInherited = ChildTypeReferences.All(d => d.IsInherited);
    }

    public static string MakeNameNoPrefix(string name)
    {
        string result = name;

        const string suffix = "TypeReference";
        if (result.EndsWith(suffix, StringComparison.Ordinal))
        { result = result.Substring(0, result.Length - suffix.Length); }

        return result;
    }

    public string NameNoPrefix => MakeNameNoPrefix(Name);

    public string TemporaryVariableName
    {
        get
        {
            string nameNoPrefix = NameNoPrefix;
            return $"{Char.ToLowerInvariant(nameNoPrefix[0])}{nameNoPrefix.Substring(1)}Type";
        }
    }

    public static string MakeTransformMethodName(string name)
        => $"Transform{name}";

    public string TransformMethodName => MakeTransformMethodName(Name);

    public string TransformChildrenMethodName => $"Transform{Name}Children";
}
