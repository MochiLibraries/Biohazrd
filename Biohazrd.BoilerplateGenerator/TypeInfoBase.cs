using Microsoft.CodeAnalysis;
using System;

namespace Biohazrd.BoilerplateGenerator;

internal abstract record TypeInfoBase
{
    public bool IsExternal { get; }
    public string? Namespace { get; }
    public string Name { get; }
    public string ParentName { get; }
    public string? ParentNamespace { get; }
    public int DistanceFromBase { get; }

    protected TypeInfoBase(ITypeSymbol symbol, int distanceFromBase, bool isExternal)
    {
        IsExternal = isExternal;
        Namespace = symbol.ContainingNamespace?.ToDisplayString();
        Name = symbol.Name;
        ParentName = symbol.BaseType?.Name ?? throw new InvalidOperationException("Symbol does not represent a valid declaration type.");
        ParentNamespace = symbol.BaseType?.ContainingNamespace?.ToDisplayString();
        DistanceFromBase = distanceFromBase;
    }
}
