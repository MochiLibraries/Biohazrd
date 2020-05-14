using ClangSharp;
using ClangSharp.Interop;
using System;
using System.IO;

namespace ClangSharpTest2020
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            DoTest();
            Console.WriteLine();
            Console.WriteLine("Done.");
            //Console.ReadLine();
        }

        private static void DoTest()
        {
            const string sourceFilePath = @"C:\Development\Playground\CppWrappingInlineMaybe\CppWrappingInlineMaybe\Source.h";

            string[] clangCommandLineArgs =
            {
                "--language=c++",
                "--std=c++17",
                "-Wno-pragma-once-outside-header", // Since we might be parsing headers, this warning will be irrelevant.
            };

            // These are the flags used by ClangSharp.PInvokeGenerator, so we're just gonna use them for now.
            CXTranslationUnit_Flags translationFlags =
                CXTranslationUnit_Flags.CXTranslationUnit_IncludeAttributedTypes |
                CXTranslationUnit_Flags.CXTranslationUnit_VisitImplicitAttributes
            ;

            CXIndex index = CXIndex.Create(displayDiagnostics: true);
            CXTranslationUnit unitHandle;
            CXErrorCode status = CXTranslationUnit.TryParse(index, sourceFilePath, clangCommandLineArgs, ReadOnlySpan<CXUnsavedFile>.Empty, translationFlags, out unitHandle);

            if (status != CXErrorCode.CXError_Success)
            {
                Console.Error.WriteLine($"Failed to parse due to {status}.");
                return;
            }

            if (unitHandle.NumDiagnostics != 0)
            {
                bool hasErrors = false;
                Console.Error.WriteLine("Compilation diagnostics:");
                for (uint i = 0; i < unitHandle.NumDiagnostics; i++)
                {
                    using CXDiagnostic diagnostic = unitHandle.GetDiagnostic(i);
                    Console.WriteLine($"    {diagnostic.Severity}: {diagnostic.Format(CXDiagnostic.DefaultDisplayOptions)}");

                    if (diagnostic.Severity == CXDiagnosticSeverity.CXDiagnostic_Error || diagnostic.Severity == CXDiagnosticSeverity.CXDiagnostic_Fatal)
                    { hasErrors = true; }
                }

                if (hasErrors)
                {
                    Console.Error.WriteLine("Aborting due to previous errors.");
                    return;
                }
            }

            using TranslationUnit unit = TranslationUnit.GetOrCreate(unitHandle);
            //new ClangWalker().VisitTranslationUnit(unit.TranslationUnitDecl);
            using var writer = new StreamWriter("Output.txt");
            Writer = writer;
            Dump(unit.TranslationUnitDecl);
        }

        private static StreamWriter Writer;

        private static void Dump(Cursor cursor)
        {
            // Skip cursors which come from included files
            // (Can also skip ones from system files. Unclear how Clang determines "system header" -- Might just be <> headers?)
            // For some reason the first declaration in a file will only have its end marked as being from the main file.
            if (!cursor.Extent.Start.IsFromMainFile && !cursor.Extent.End.IsFromMainFile)
            {
                return;
            }

            // Some types of cursors are never relevant
            bool skip = false;

            if (cursor is AccessSpecDecl)
            { skip = true; }

            if (skip)
            {
                if (cursor.CursorChildren.Count > 0)
                { WriteLine("THE FOLLOWING CURSOR WAS GONNA BE SKIPPED BUT IT HAS CHILDREN!"); }
                else
                { return; }
            }

            string extra = "";
            {
                string mangling = cursor.Handle.Mangling.ToString();
                if (!string.IsNullOrEmpty(mangling))
                {
                    extra += $" Mangled={mangling}";
                }

                if (cursor is FunctionDecl function)
                {
                    if (function.IsInlined)
                    { extra += " INLINE"; }
                }

#if false
                if (cursor.Extent.Start.IsFromMainFile)
                { extra += " MAIN"; }
                else if (cursor.Extent.End.IsFromMainFile)
                { extra += " MAIN(END)"; }

                if (cursor.Extent.Start.IsInSystemHeader)
                { extra += " SYS"; }
                else if (cursor.Extent.End.IsInSystemHeader)
                { extra += " SYS(END)"; }
#endif
            }

            WriteLine($"{cursor.CursorKindSpellingSafe()} - {cursor.Spelling}{extra}");

            // Clang seems to have a basic understanding of Doxygen comments.
            // This seems to associate the comment as appropriate for prefix and postfix documentation. Pretty neat!
            string commentText = clang.Cursor_getRawCommentText(cursor.Handle).ToString();
            if (!String.IsNullOrEmpty(commentText))
            { WriteLine(commentText); }

#if false
            if (cursor.Extent.Start.IsFromMainFile != cursor.Extent.End.IsFromMainFile)
            {
                WriteLine("--------------");
                WriteLine("Start and end location do not agree whether this cursor is in the main file!");
                WriteLine($"Start: {cursor.Extent.Start}");
                WriteLine($"  End: {cursor.Extent.End}");
                WriteLine("--------------");
            }
#endif

            Cursor cursorToIgnore = null;
            {
                if (cursor is FunctionDecl function)
                { cursorToIgnore = function.Body; }
                else if (cursor is FieldDecl && cursor.CursorChildren.Count == 1)
                { cursorToIgnore = cursor.CursorChildren[0]; }
            }

            Indent();
            foreach (Cursor child in cursor.CursorChildren)
            {
                if (child == cursorToIgnore)
                { continue; }

                Dump(child);
            }
            Unindent();
        }

        private static int IndentLevel = 0;

        private static void Indent()
            => IndentLevel++;

        private static void Unindent()
            => IndentLevel--;

        private static void WriteLine(string message)
        {
            for (int i = 0; i < IndentLevel; i++)
            { Writer.Write("  "); }

            Writer.WriteLine(message);
        }
    }
}
