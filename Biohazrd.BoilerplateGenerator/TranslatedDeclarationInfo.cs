using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Biohazrd.BoilerplateGenerator;

internal sealed record TranslatedDeclarationInfo : TypeInfoBase
{
    public ImmutableArray<PropertyInfo> ChildDeclarations { get; }
    public ImmutableArray<PropertyInfo> ChildTypeReferences { get; }

    public bool AllChildDeclarationsAreInherited { get; }
    public bool AllChildTypeReferencesAreInherited { get; }

    public TranslatedDeclarationInfo(ITypeSymbol symbol, int distanceFromBase, bool isExternal)
        : base(symbol, distanceFromBase, isExternal)
    {
        ImmutableArray<PropertyInfo>.Builder childDeclarations = ImmutableArray.CreateBuilder<PropertyInfo>();
        ImmutableArray<PropertyInfo>.Builder childTypeReferences = ImmutableArray.CreateBuilder<PropertyInfo>();

        bool isInherited = false;
        for (ITypeSymbol? sourceSymbol = symbol; sourceSymbol != null && !sourceSymbol.IsTranslatedDeclaration(); sourceSymbol = sourceSymbol.BaseType)
        {
            foreach (ISymbol member in sourceSymbol.GetMembers())
            {
                if (member is not IPropertySymbol property)
                { continue; }

                PropertyInfo.TryCreate(property, childDeclarations, childTypeReferences, isInherited);
            }

            isInherited = true;
        }

        ChildDeclarations = childDeclarations.MoveToImmutableSafe();
        ChildTypeReferences = childTypeReferences.MoveToImmutableSafe();

        AllChildDeclarationsAreInherited = ChildDeclarations.All(d => d.IsInherited);
        AllChildTypeReferencesAreInherited = ChildTypeReferences.All(d => d.IsInherited);
    }

    public static string MakeNameNoPrefix(string name)
    {
        string result = name;

        const string prefix = "Translated";
        if (result.StartsWith(prefix, StringComparison.Ordinal))
        { result = result.Substring(prefix.Length); }

        return result;
    }

    public string NameNoPrefix => MakeNameNoPrefix(Name);

    public string TemporaryVariableName
    {
        get
        {
            string nameNoPrefix = NameNoPrefix;
            return $"{Char.ToLowerInvariant(nameNoPrefix[0])}{nameNoPrefix.Substring(1)}Declaration";
        }
    }

    public static string MakeVisitMethodName(string name)
        => $"Visit{MakeNameNoPrefix(name)}";

    public string VisitMethodName => MakeVisitMethodName(Name);

    public static string MakeTransformMethodName(string name)
        => $"Transform{MakeNameNoPrefix(name)}";

    public string TransformMethodName => MakeTransformMethodName(Name);
    public string TransformChildrenMethodName => $"Transform{NameNoPrefix}Children";
    public string TransformTypeReferencesMethodName => $"Transform{NameNoPrefix}TypeReferences";

}
