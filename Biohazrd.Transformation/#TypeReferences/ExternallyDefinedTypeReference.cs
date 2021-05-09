using Biohazrd.Transformation.Infrastructure;

namespace Biohazrd.Transformation
{
    public sealed record ExternallyDefinedTypeReference : TypeReference, ICustomTypeReference
    {
        public string? Namespace { get; }
        public string Name { get; }
        public bool ShouldSanitize { get; init; } = false;

        public ExternallyDefinedTypeReference(string? namespaceName, string name)
        {
            Namespace = namespaceName;
            Name = name;
        }

        public ExternallyDefinedTypeReference(string name)
            : this(null, name)
        { }

        TypeTransformationResult ICustomTypeReference.TransformChildren(ITypeTransformation transformation, TypeTransformationContext context)
            => this;

        public override string ToString()
            => Namespace is not null ? $"{Namespace}.{Name}" : Name;
    }
}
