using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Diagnostics;
using System.Reflection;
using Type = System.Type;

namespace ClangSharpTest2020
{
    internal static class ClangSharpExtensions
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

        public static bool IsFromMainFile(this Cursor cursor)
            => cursor.Extent.IsFromMainFile();

        public static bool IsFromMainFile(this CXSourceRange extent)
        {
#if false
            // This property uses libclang's clang_Location_isFromMainFile which in turn uses SourceManager::isWrittenInMainFile
            // This method has some quirks:
            // * It considered cursors which are the result of a macro expansion to have come from outside of the file.
            //  * While technically true, this isn't what we actually want in our case. (Our main motivation is to skip over cursors from included files.)
            // * For some reason the first declaration in a file will only have its end marked as being from the main file, so we check both.
            //  * This happens with some, but not all, cursors created from a macro expansion.
            return extent.Start.IsFromMainFile || extent.End.IsFromMainFile;
#else
            // Unlike clang_Location_isFromMainFile, pathogen_Location_isFromMainFile uses SourceManager::isInMainFile, which does not suffer from the previously mentioned quirks.
            // One downside of it, however, is that it considered builtin macros to be from the main file when CXTranslationUnit_DetailedPreprocessingRecord is enabled.
            // These preprocessor entities look like this:
            //   MacroDefinitionRecord MacroDefinition - __llvm__
            //      From main file: False -- False
            //     From main file2: True -- True
            //       From sys file: True -- True
            //           Expansion: :2:9..19[27..37]
            //       Instantiation: :2:9..19[27..37]
            //            Spelling: :2:9..19[27..37]
            //                File: :2:9..19[27..37]
            //            Presumed: <built-in>:1:9..19
            // --------------------------------------------------------------
            // As such, we check if the cursor comes from a system file first to early-reject it.
            if (extent.Start.IsInSystemHeader || extent.End.IsInSystemHeader)
            { return false; }

            bool isStartInMain = extent.Start.IsFromMainFilePathogen();
            bool isEndInMain = extent.End.IsFromMainFilePathogen();
            Debug.Assert(isStartInMain == isEndInMain, "Both the start and end of a cursor should be in or out of main.");
            return isStartInMain || isEndInMain;
#endif
        }

        public static bool IsFromMainFilePathogen(this CXSourceLocation location)
            => PathogenExtensions.pathogen_Location_isFromMainFile(location) != 0;

        private static MethodInfo TranslationUnit_GetOrCreate;
        [ThreadStatic] private static object[] TranslationUnit_GetOrCreate_Parameters;
        public static Cursor GetOrCreate(this TranslationUnit translationUnit, CXCursor handle)
        {
            if (handle.TranslationUnit != translationUnit.Handle)
            { throw new ArgumentException("The specified cursor is not from the specified translation unit.", nameof(handle)); }

            if (TranslationUnit_GetOrCreate == null)
            {
                Type[] parameterTypes = { typeof(CXCursor) };
                const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DoNotWrapExceptions;
                MethodInfo getOrCreateGeneric = typeof(TranslationUnit).GetMethod("GetOrCreate", genericParameterCount: 1, bindingFlags, binder: null, parameterTypes, modifiers: null);

                if (getOrCreateGeneric is null)
                { throw new NotSupportedException("Could not get the GetOrCreate<TCursor>(CXCursor) method!"); }

                TranslationUnit_GetOrCreate = getOrCreateGeneric.MakeGenericMethod(typeof(Cursor));
            }

            if (TranslationUnit_GetOrCreate_Parameters == null)
            { TranslationUnit_GetOrCreate_Parameters = new object[1]; }

            TranslationUnit_GetOrCreate_Parameters[0] = handle; //PERF: Reuse the box
            return (Cursor)TranslationUnit_GetOrCreate.Invoke(translationUnit, TranslationUnit_GetOrCreate_Parameters);
        }
    }
}
