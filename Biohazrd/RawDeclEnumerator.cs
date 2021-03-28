using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using static ClangSharp.Pathogen.PathogenExtensions;

namespace Biohazrd
{
    public struct RawDeclEnumerator : IEnumerator<Decl>
    {
        public Decl Current { get; private set; }

        object IEnumerator.Current => Current;

        internal RawDeclEnumerator(Decl parentDeclaration)
        {
            Current = parentDeclaration;
            IsFirstMoveNext = true;
        }

        private bool IsFirstMoveNext;
        public bool MoveNext()
        {
            if (Current is null)
            { return false; }

            CXCursor next;
            if (IsFirstMoveNext)
            {
                next = pathogen_BeginEnumerateDeclarationsRaw(Current.Handle);
                IsFirstMoveNext = false;
            }
            else
            { next = pathogen_EnumerateDeclarationsRawMoveNext(Current.Handle); }

            if (next.IsNull)
            {
                Current = null!;
                return false;
            }

            Cursor nextCursor = Current.TranslationUnit.FindCursor(next);
            Debug.Assert(nextCursor is Decl, "Only declarations should be enumerated!");
            Current = (Decl)nextCursor;
            return true;
        }

        void IEnumerator.Reset()
            => throw new NotSupportedException();

        void IDisposable.Dispose()
        { }
    }
}
