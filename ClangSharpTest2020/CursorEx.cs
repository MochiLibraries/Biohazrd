using System;
using System.Collections.Generic;
using System.Text;
using ClangSharp;
using ClangSharp.Interop;

namespace ClangSharpTest2020
{
    internal static class CursorEx
    {
        public static string CursorKindSpellingSafe(this Cursor cursor)
        {
            string ret = cursor.CursorKindSpelling;

            // A handful of the kinds are incorrectly exposed as Unexposed.
            if (ret == "UnexposedDecl")
            {
                if (cursor.CursorKind == CXCursorKind.CXCursor_UnexposedDecl)
                { ret = cursor.Handle.DeclKind.ToString(); } // Sometimes the cursor doesn't know the kind, but the declaration does.
                else
                { ret = cursor.CursorKind.ToString(); }
            }

            return ret;
        }
    }
}
