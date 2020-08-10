using System.Collections;
using System.Collections.Generic;

namespace ClangSharpTest2020
{
    public interface IDeclarationContainer : IEnumerable<TranslatedDeclaration>
    {
        TranslatedLibrary Library { get; }
        TranslatedFile File { get; }

        //TODO: These should really not be exposed publicly
        void AddDeclaration(TranslatedDeclaration declaration);
        void RemoveDeclaration(TranslatedDeclaration declaration);
        string GetNameForUnnamed(string category);

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}
