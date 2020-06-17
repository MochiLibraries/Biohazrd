using System.Collections.Immutable;

namespace ClangSharpTest2020
{
    public abstract class TranslatedDeclaration
    {
        protected ImmutableArray<TranslationContext> Context { get; }
        internal TranslatedFile File { get; } //TODO: This should probably be protected
        public abstract string TranslatedName { get; }

        protected TranslatedDeclaration(ImmutableArray<TranslationContext> context, TranslatedFile file)
        {
            Context = context;
            File = file;
        }

        public abstract void Translate(CodeWriter writer);

        public override string ToString()
            => TranslatedName;
    }
}
