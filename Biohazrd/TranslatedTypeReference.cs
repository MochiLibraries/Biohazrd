using ClangType = ClangSharp.Type;

namespace Biohazrd
{
    public struct TranslatedTypeReference
    {
        public ClangType Type { get; }
        public bool MustBePassedByReference { get; }

        public TranslatedTypeReference(ClangType type)
        {
            Type = type;
            MustBePassedByReference = type.MustBePassedByReference();
        }
    }
}
