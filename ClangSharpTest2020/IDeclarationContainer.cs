using System.Collections;
using System.Collections.Generic;

namespace ClangSharpTest2020
{
    internal interface IDeclarationContainer : IEnumerable<TranslatedDeclaration>
    {
        TranslatedFile File { get; }
        void AddDeclaration(TranslatedDeclaration declaration);
        void RemoveDeclaration(TranslatedDeclaration declaration);
        string GetNameForUnnamed(string category);

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}
