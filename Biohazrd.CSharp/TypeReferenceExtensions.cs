namespace Biohazrd.CSharp
{
    internal static class TypeReferenceExtensions
    {
        public static bool IsCSharpType(this TypeReference typeReference, CSharpBuiltinType cSharpType)
            => typeReference is CSharpBuiltinTypeReference cSharpTypeReference && cSharpTypeReference.Type == cSharpType;
    }
}
