using ClangSharp.Interop;
using ClangType = ClangSharp.Type;

namespace Biohazrd
{
    public record ClangTypeReference : TypeReference
    {
        private ClangType _ClangType;
        public ClangType ClangType
        {
            get => _ClangType;
            init
            {
                _ClangType = value;
                MustBePassedByReference = value.MustBePassedByReference();
            }
        }

        public ClangTypeReference(ClangType clangType)
        {
            _ClangType = null!; // This will be assigned when ClangType is assigned.
            ClangType = clangType;
        }

        internal ClangTypeReference(TranslationUnitParser parsingContext, CXType clangType)
            : this(parsingContext.FindType(clangType))
        { }

        public override string ToString()
            => $"`{ClangType}`";
    }
}
