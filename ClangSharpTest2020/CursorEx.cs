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

        public static string CursorKindDetailed(this Cursor cursor)
            => $"{cursor.CursorKindSpellingSafe()} ({cursor.GetType().Name})";

        //TODO: This method is somewhat short-sighted.
        // It'll detect cursors included cursors (what we want), but it'll also detect cursors that came from macros in other files.
        // For instance, this considers the methods added by PxFlags.h's PX_FLAGS_OPERATORS to come from outside the main file.
        // While technically true, this usually isn't what we want.
        public static bool IsFromMainFile(this Cursor cursor)
            // For some reason the first declaration in a file will only have its end marked as being from the main file, so we check both.
            => cursor.Extent.Start.IsFromMainFile || cursor.Extent.End.IsFromMainFile;
    }
}
