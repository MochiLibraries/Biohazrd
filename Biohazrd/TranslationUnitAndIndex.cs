using ClangSharp;
using ClangSharp.Interop;
using System;

namespace Biohazrd
{
    internal sealed class TranslationUnitAndIndex : IDisposable
    {
        private readonly CXIndex Index;
        public TranslationUnit TranslationUnit { get; }

        internal TranslationUnitAndIndex(CXIndex index, TranslationUnit translationUnit)
        {
            Index = index;
            TranslationUnit = translationUnit;
        }

        private bool IsDisposed = false;
        public void Dispose()
        {
            if (IsDisposed)
            { return; }

            // The translation unit _must_ be disposed before the index
            // If TranslationUnit's finalizer runs before us, this will be a no-op.
            TranslationUnit?.Dispose();

            if (Index.Handle != default)
            { Index.Dispose(); }

            GC.SuppressFinalize(this);
            IsDisposed = true;
        }

        ~TranslationUnitAndIndex()
            => Dispose();
    }
}
