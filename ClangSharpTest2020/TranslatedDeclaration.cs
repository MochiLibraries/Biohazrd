using System.Collections.Immutable;

namespace ClangSharpTest2020
{
    public abstract class TranslatedDeclaration
    {
        internal TranslatedFile File { get; } //TODO: This should probably be protected
        public abstract string TranslatedName { get; }

        protected TranslatedDeclaration(TranslatedFile file)
        {
            File = file;
        }

        public abstract void Translate(CodeWriter writer);

        public override string ToString()
            => TranslatedName;
    }
}
