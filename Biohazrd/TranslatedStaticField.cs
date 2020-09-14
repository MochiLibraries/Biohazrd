using ClangSharp;

namespace Biohazrd
{
    /// <summary>A translated static field or global variable.</summary>
    public sealed record TranslatedStaticField : TranslatedDeclaration
    {
        public TypeReference Type { get; init; }

        public string DllFileName { get; init; } = "TODO.dll";
        public string MangledName { get; init; }
        
        internal TranslatedStaticField(TranslatedFile file, VarDecl variable)
            : base(file, variable)
        {
            Type = new ClangTypeReference(variable.Type);
            MangledName = variable.Handle.Mangling.ToString();

            // Static variables outside of records should always be public.
            if (variable.CursorParent is not RecordDecl)
            { Accessibility = AccessModifier.Public; }
        }

        public override string ToString()
            => $"Static Field {base.ToString()}";
    }
}
