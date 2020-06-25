namespace ClangSharpTest2020
{
    public abstract class TranslationTransformation
    {
        public abstract void Apply();

        public delegate TranslationTransformation FactoryDelegate(TranslatedDeclaration declaration);
    }
}
