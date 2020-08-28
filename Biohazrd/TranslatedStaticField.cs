using ClangSharp;

namespace Biohazrd
{
    /// <summary>A translated static field or global variable.</summary>
    public sealed record TranslatedStaticField : TranslatedDeclaration
    {
        public TranslatedTypeReference Type { get; init; }
        
        internal TranslatedStaticField(TranslatedFile file, VarDecl variable)
            : base(file, variable)
        {
            // Static variables outside of records should always be public.
            if (variable.CursorParent is not RecordDecl)
            { Accessibility = AccessModifier.Public; }
        }
    }
}
