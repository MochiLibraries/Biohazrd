using ClangSharp;
using System.Collections.Immutable;

namespace ClangSharpTest2020
{
    public sealed class TranslatedFunction
    {
        public ImmutableArray<TranslationContext> Context { get; }
        public TranslatedFile File { get; }
        public TranslatedRecord Record { get; }
        public FunctionDecl Function { get; }

        internal TranslatedFunction(ImmutableArray<TranslationContext> context, TranslatedFile file, FunctionDecl function)
        {
            Context = context;
            File = file;
            Record = null;
            Function = function;
        }

        internal TranslatedFunction(ImmutableArray<TranslationContext> context, TranslatedRecord record, FunctionDecl function)
        {
            Context = context;
            File = record.File;
            Record = record;
            Function = function;
        }

        public void Translate(CodeWriter writer)
        {
            //TODO
            writer.EnsureSeparation();
            writer.WriteLine($"//TODO: Translate {Function}");
        }

        public override string ToString()
            => Function.Name;
    }
}
