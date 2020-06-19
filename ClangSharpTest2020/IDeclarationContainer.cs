namespace ClangSharpTest2020
{
    internal interface IDeclarationContainer
    {
        TranslatedFile File { get; }
        void AddDeclaration(TranslatedDeclaration declaration);
        void RemoveDeclaration(TranslatedDeclaration declaration);
        string GetNameForUnnamed(string category);
    }
}
