using Biohazrd.CSharp;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;

namespace Biohazrd.BoilerplateGenerator;
internal sealed record PropertyInfo
{
    public PropertyKind Kind { get; }
    public string? TypeNamespace { get; }
    public string TypeName { get; }
    public string Name { get; }
    public bool IsCatchAllMembersProperty { get; }
    /// <summary>True if the type of this property represents either TranslatedDeclaration or TypeReference.</summary>
    public bool TypeIsBiohazrdBase { get; }
    public bool IsInherited { get; }

    private PropertyInfo(PropertyKind kind, ITypeSymbol type, string name, bool isCatchAllMembersProperty, bool typeIsBiohazrdBase, bool isInherited)
    {
        Kind = kind;
        TypeNamespace = type.ContainingNamespace?.ToDisplayString();
        TypeName = type.Name;
        Name = name;
        IsCatchAllMembersProperty = isCatchAllMembersProperty;
        TypeIsBiohazrdBase = typeIsBiohazrdBase;
        IsInherited = isInherited;
    }

    public static void TryCreate(IPropertySymbol symbol, ImmutableArray<PropertyInfo>.Builder? declarationProperties, ImmutableArray<PropertyInfo>.Builder typeReferenceProperties, bool isInherited)
    {
        PropertyKind kind = symbol.NullableAnnotation == NullableAnnotation.Annotated ? PropertyKind.SingleNullableValue : PropertyKind.SingleValue;
        ITypeSymbol effectiveType = symbol.Type;
        bool currentIsTopLevelType = true;

        PropertyInfo MakePropertyInfo()
        {
            bool isCatchAllMembersProperty = false;

            foreach (AttributeData attribute in symbol.GetAttributes())
            {
                if (attribute.AttributeClass.IsCatchAllMembersPropertyAttribute())
                {
                    isCatchAllMembersProperty = true;
                    break;
                }
            }

            return new PropertyInfo(kind, effectiveType, symbol.Name, isCatchAllMembersProperty, currentIsTopLevelType, isInherited);
        }

        for (ITypeSymbol? type = symbol.Type; type is not null; type = type.BaseType, currentIsTopLevelType = false)
        {
            if (type.IsImmutableArray(out ITypeSymbol? arrayElementType))
            {
                if (kind != PropertyKind.SingleValue)
                { return; }

                kind = PropertyKind.ImmutableArray;
                effectiveType = type = arrayElementType;
                currentIsTopLevelType = true;
            }
            else if (type.IsImmutableList(out ITypeSymbol? listElementType))
            {
                if (kind != PropertyKind.SingleValue)
                { return; }

                kind = PropertyKind.ImmutableList;
                effectiveType = type = listElementType;
                currentIsTopLevelType = true;
            }

            if (type.IsTranslatedDeclaration())
            {
                if (declarationProperties is null)
                { return; }

                declarationProperties.Add(MakePropertyInfo());
                return;
            }
            else if (type.IsTypeReference())
            {
                if (typeReferenceProperties is null)
                { return; }

                typeReferenceProperties.Add(MakePropertyInfo());
                return;
            }
        }
    }

    public string ElementVariableName
    {
        get
        {
            string result = Name;
            if (result.Length > 0)
            { result = $"{char.ToLowerInvariant(result[0])}{result.Substring(1)}"; }

            const string ies = "ies";
            if (result.EndsWith(ies))
            { result = $"{result.Substring(0, result.Length - ies.Length)}y"; }
            else if (result.EndsWith("s"))
            { result = result.Substring(0, result.Length - 1); }

            return result;
        }
    }

    public void WriteTransformationResultAccess(CSharpCodeWriter writer, bool withNullCheck = true)
    {
        switch (Kind)
        {
            case PropertyKind.SingleValue:
            case PropertyKind.SingleNullableValue:
                writer.Write("NewValue");

                if (withNullCheck && Kind == PropertyKind.SingleValue)
                {
                    writer.Using("System"); // InvalidOperationException
                    writer.Write($" ?? throw new InvalidOperationException($\"Tried to set {Name} to null, but it is not optional in this context.\")");
                }
                return;
            case PropertyKind.ImmutableArray:
                writer.Write("MoveToImmutable()");
                return;
            case PropertyKind.ImmutableList:
                writer.Write("ToImmutable()");
                return;
            default:
                throw new InvalidOperationException($"Invalid property kind '{Kind}'.");
        }
    }
}
