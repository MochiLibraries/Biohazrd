using ClangSharp;
using System;
using System.Collections.Immutable;

namespace Biohazrd
{
    public sealed record TranslatedLibrary : IDisposable
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

        internal TranslatedDeclaration TryFindTranslation(Decl declaration)
            => throw new NotImplementedException(); //TODO Debug.Fail

        public void Dispose()
            => TranslationUnitAndIndex?.Dispose();
    }
}
