namespace Biohazrd.CSharp
{
    public sealed record CSharpBuiltinTypeReference : TypeReference
    {
        public CSharpBuiltinType Type { get; }

        internal CSharpBuiltinTypeReference(CSharpBuiltinType type)
            => Type = type;
    }
}
