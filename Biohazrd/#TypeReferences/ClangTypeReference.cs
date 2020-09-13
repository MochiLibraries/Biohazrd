using ClangSharp.Interop;
using ClangType = ClangSharp.Type;

namespace Biohazrd
{
    public record ClangTypeReference : TypeReference
    {
        public ClangType ClangType { get; init; }

        public ClangTypeReference(ClangType clangType)
            => ClangType = clangType;

        internal ClangTypeReference(TranslationUnitParser parsingContext, CXType clangType)
            : this(parsingContext.FindType(clangType))
        { }

        public override string ToString()
            => $"`{ClangType}`";
    }
}
