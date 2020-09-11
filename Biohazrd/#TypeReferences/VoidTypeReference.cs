namespace Biohazrd
{
    /// <summary>Represents the <c>void</c> type.</summary>
    public record VoidTypeReference : TypeReference
    {
        private VoidTypeReference()
        { }

        public static readonly VoidTypeReference Instance = new VoidTypeReference();

        public override string ToString()
            => "void";
    }
}
