namespace Biohazrd
{
    /// <summary>Represents a pointer of another type.</summary>
    public record PointerTypeReference : TypeReference
    {
        /// <summary>The type pointed to by this one.</summary>
        public TypeReference Inner { get; init; }

        /// <summary>True when this pointer represents what used to be a C++-style reference type.</summary>
        public bool WasReference { get; init; }

        public PointerTypeReference(TypeReference inner)
            => Inner = inner;

        public override string ToString()
            => WasReference ? $"{Inner}&" : $"{Inner}*";
    }
}
