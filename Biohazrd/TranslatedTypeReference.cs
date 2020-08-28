using ClangType = ClangSharp.Type;

namespace Biohazrd
{
    public struct TranslatedTypeReference
    {
        public ClangType Type { get; }
        public bool MustBePassedByReference { get; }

        internal TranslatedTypeReference(ClangType type)
        {
            Type = type;
            MustBePassedByReference = type.MustBePassedByReference();
        }
    }
}
