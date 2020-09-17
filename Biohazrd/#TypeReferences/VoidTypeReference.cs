namespace Biohazrd
{
    /// <summary>Represents the <c>void</c> type.</summary>
    public record VoidTypeReference : TypeReference
    {
        private VoidTypeReference()
        { }

        public static readonly VoidTypeReference Instance = new VoidTypeReference();
        public static readonly PointerTypeReference PointerInstance = new PointerTypeReference(Instance);

        public override string ToString()
            => "void";
    }
}
