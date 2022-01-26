namespace Biohazrd.CSharp
{
    internal static class TypeReferenceExtensions
    {
        /// <summary>Checks if the type reference is equivalent to the specified <paramref name="cSharpType"/>.</summary>
        /// <remarks>This method is meant for use by the C# output generation stage, so it assumes typedefs are no longer meaningful and resolves them as needed.</remarks>
        internal static bool IsCSharpType(this TypeReference typeReference, TranslatedLibrary library, CSharpBuiltinType cSharpType)
        {
            switch (typeReference)
            {
                case CSharpBuiltinTypeReference cSharpTypeReference:
                    return cSharpTypeReference.Type == cSharpType;
                // If the type reference refers to a typedef, check if it points to the specified C# type.
                case TranslatedTypeReference translatedTypeReference:
                    if (translatedTypeReference.TryResolve(library) is TranslatedTypedef typedef)
                    { return IsCSharpType(typedef.UnderlyingType, library, cSharpType); }
                    else
                    { return false; }
                default:
                    return false;
            }
        }
    }
}
