using ClangSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Biohazrd
{
    public sealed record TranslatedLibrary : IDisposable, IEnumerable<TranslatedDeclaration>
    {
        private readonly TranslationUnitAndIndex TranslationUnitAndIndex;

        public ImmutableList<TranslatedDeclaration> Declarations { get; init; }
        public ImmutableArray<TranslatedFile> Files { get; }
        public ImmutableArray<TranslationDiagnostic> ParsingDiagnostics { get; }

        internal TranslatedLibrary
        (
            TranslationUnitAndIndex translationUnitAndIndex,
            ImmutableArray<TranslatedFile> files,
            ImmutableArray<TranslationDiagnostic> parsingDiagnostics,
            ImmutableList<TranslatedDeclaration> declarations
        )
        {
            TranslationUnitAndIndex = translationUnitAndIndex;
            Declarations = declarations;
            Files = files;
            ParsingDiagnostics = parsingDiagnostics;
        }

        public IEnumerator<TranslatedDeclaration> GetEnumerator()
            => Declarations.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        internal TranslatedDeclaration TryFindTranslation(Decl declaration)
            => throw new NotImplementedException(); //TODO Debug.Fail

        public void Dispose()
            => TranslationUnitAndIndex?.Dispose();
    }
}
