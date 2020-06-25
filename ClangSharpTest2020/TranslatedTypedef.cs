using ClangSharp;

namespace ClangSharpTest2020
{
    public sealed class TranslatedTypedef : TranslatedDeclaration
    {
        public TypedefDecl Typedef { get; }

        public override string DefaultName { get; }
        public override bool CanBeRoot => true;
        public override bool IsDummy => true;

        internal TranslatedTypedef(IDeclarationContainer container, TypedefDecl typedef)
            : base(container)
        {
            Typedef = typedef;
            Declaration = Typedef;
            Accessibility = Typedef.Access.ToTranslationAccessModifier();
            DefaultName = Typedef.Name;
        }

        protected override void TranslateImplementation(CodeWriter writer)
        {
            if (GlobalConfiguration.DumpClangDetails)
            { writer.WriteLine($"// typedef '{Typedef.UnderlyingType}' '{this}'"); }
        }
    }
}
