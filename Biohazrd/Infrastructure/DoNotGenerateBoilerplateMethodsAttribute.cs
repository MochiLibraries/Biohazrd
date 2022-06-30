using System;

namespace Biohazrd.Infrastructure;

/// <summary>Indicates that that transformation/visitor boilerplate should not be generated for the given type.</summary>
/// <remarks>
/// Note: This attribute currently has no effect on custom declarations.
///
/// This attribute indicates that Biohazrd's boilerplate source generator should ignore this type.
///
/// This attribute is only valid on types derived from either <see cref="TranslatedDeclaration"/> or <see cref="TypeReference"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DoNotGenerateBoilerplateMethodsAttribute : Attribute
{ }
