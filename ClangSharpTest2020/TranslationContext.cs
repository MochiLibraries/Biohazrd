using ClangSharp;

namespace ClangSharpTest2020
{
    public struct TranslationContext
    {
        public readonly Cursor Cursor;
        public readonly TranslationContextKind Kind;

        public TranslationContext(RecordDecl record)
        {
            Cursor = record;
            Kind = TranslationContextKind.Record;
        }

        public TranslationContext(NamespaceDecl namespaceDeclaration)
        {
            Cursor = namespaceDeclaration;
            Kind = TranslationContextKind.Namespace;
        }

        public static implicit operator TranslationContext(RecordDecl record)
            => new TranslationContext(record);

        public static implicit operator TranslationContext(NamespaceDecl namespaceDeclaration)
            => new TranslationContext(namespaceDeclaration);

        public override string ToString()
            => $"{Kind} context for {Cursor}";
    }
}
