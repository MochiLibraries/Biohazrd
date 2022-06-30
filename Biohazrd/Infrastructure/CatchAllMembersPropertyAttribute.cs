using System;
using System.Collections.Immutable;

namespace Biohazrd.Infrastructure;

/// <summary>Indicates that the specified property is the target for mistmatched and excees declarations resulting from the transformation of a sibling property.</summary>
/// <remarks>
/// Note: This attribute's functionality is not available for custom declarations, but it can still be used to indicate intent.
///
/// When a <see cref="TranslatedDeclaration"/> property is transformed, the transformation can specify any number or type of declarations in its place.
/// However, some properties are more specifically typed than <see cref="TranslatedDeclaration"/> and may not allow multiple elements.
///
/// When transformation of these properties yields multiple declarations or any of a mismatched type, the transformation infrastructure has to determine what to do with the misfit properties.
///
/// If the declaration has a property marked with this attribute, that property receives the misfit properties, otherwise a runtime error occurs.
///
/// This attribute is only valid on a single property of <see cref="ImmutableArray{T}"/> or <see cref="ImmutableList{T}"/> with an element type of <see cref="TranslatedDeclaration"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CatchAllMembersPropertyAttribute : Attribute
{ }
