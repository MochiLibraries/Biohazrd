using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using ClangType = ClangSharp.Type;

namespace ClangSharpTest2020
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            NativeLibrary.SetDllImportResolver(typeof(Program).Assembly, ImportResolver);

            DoTest();
            Console.WriteLine();
            Console.WriteLine("Done.");
            Console.ReadLine();
        }

        private static IntPtr ImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            // Note: The debug build of libclang is weirdly unstable (when built from Visual Studio?)
            // Some issues it has:
            // * Building the debug configuration twice in a row isn't successful due to the table generator not working https://bugs.llvm.org/show_bug.cgi?id=41367
            // * I'll crash in the CRT over things that don't seem to be our fault.
            // * The AST context isn't fully initialized.
            // Note: This isn't a build system issue since it happens even when using Ninja,
            // which seems to be the workflow recommended by the official documentation https://clang.llvm.org/get_started.html
            // We might be able to build with Clang instead of MSVC to fix these issues.
            if (libraryName == "libclang.dll")
            //{ return NativeLibrary.Load(@"C:\Scratch\llvm-project\build\Debug\bin\libclang.dll"); }
            //{ return NativeLibrary.Load(@"C:\Scratch\llvm-project\build\Release\bin\libclang.dll"); }
            //{ return NativeLibrary.Load(@"C:\Scratch\llvm-project\build_ninja\bin\libclang.dll"); }
            { return NativeLibrary.Load(@"C:\Scratch\llvm-project\build_ninja-release\bin\libclang.dll"); }

            return IntPtr.Zero;
        }

        private static void DoTest()
        {
            string[] includeDirs =
            {
                @"C:\Scratch\PhysX\physx\install\vc15win64\PhysX\include\",
                @"C:\Scratch\PhysX\pxshared\include\"
            };

            List<string> _clangCommandLineArgs = new List<string>()
            {
                "-D_DEBUG",
                "--language=c++",
                "--std=c++17",
                "-Wno-pragma-once-outside-header", // Since we are parsing headers, this warning will be irrelevant.
                "-Wno-return-type-c-linkage", // PxGetFoundation triggers this. There's code to suppress it, but it's only triggered when building for Clang on Linux.
                //"--target=x86_64-pc-linux",
            };

            foreach (string includeDir in includeDirs)
            { _clangCommandLineArgs.Add($"-I{includeDir}"); }

            string[] clangCommandLineArgs = _clangCommandLineArgs.ToArray();

            CXIndex index = CXIndex.Create(displayDiagnostics: true);

            List<string> files = new List<string>();

#if false
            files.Add(@"C:\Development\Playground\CppWrappingInlineMaybe\CppWrappingInlineMaybe\Source.h");
#else
            HashSet<string> whiteListedFiles = new HashSet<string>()
            {
                "PxFoundation.h"
            };

            HashSet<string> blackListedFiles = new HashSet<string>()
            {
                "PxUnixIntrinsics.h" // Not relevant on Windows
            };

            foreach (string includeDir in includeDirs)
            {
                foreach (string headerFile in Directory.EnumerateFiles(includeDir, "*.h", SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileName(headerFile);

#if true
                    if (!whiteListedFiles.Contains(fileName))
                    { continue; }
#endif

                    if (blackListedFiles.Contains(fileName))
                    { continue; }

                    files.Add(headerFile);
                }
            }
#endif

#if false
            using var writer = new StreamWriter("Output.txt");
            Writer = writer;

            foreach (string file in files)
            {
                WriteLine("==============================================================================");
                WriteLine(file);
                Console.WriteLine(file);
                WriteLine("==============================================================================");
                if (!Translate(index, file, clangCommandLineArgs))
                { return; }
            }
#else
            using TranslatedLibrary library = new TranslatedLibrary(clangCommandLineArgs);

            foreach (string file in files)
            {
                Console.WriteLine("==============================================================================");
                Console.WriteLine(file);
                Console.WriteLine("==============================================================================");

                library.AddFile(file);

                if (library.HasErrors)
                { return; }
            }
#endif
        }

        private static bool Translate(in CXIndex index, string sourceFilePath, string[] clangCommandLineArgs)
        {
            // These are the flags used by ClangSharp.PInvokeGenerator, so we're just gonna use them for now.
            CXTranslationUnit_Flags translationFlags =
                CXTranslationUnit_Flags.CXTranslationUnit_IncludeAttributedTypes |
                CXTranslationUnit_Flags.CXTranslationUnit_VisitImplicitAttributes
            ;
            CXTranslationUnit unitHandle;
            CXErrorCode status = CXTranslationUnit.TryParse(index, sourceFilePath, clangCommandLineArgs, ReadOnlySpan<CXUnsavedFile>.Empty, translationFlags, out unitHandle);

            if (status != CXErrorCode.CXError_Success)
            {
                Console.Error.WriteLine($"Failed to parse due to {status}.");
                return false;
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
                    return false;
                }
            }

            using TranslationUnit unit = TranslationUnit.GetOrCreate(unitHandle);
            Dump(unit.TranslationUnitDecl);
            return true;
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
            {
                //if (cursor is AccessSpecDecl)
                //{ skip = true; }

                if (cursor is RecordDecl record && !record.Handle.IsDefinition)
                { skip = true; }
            }

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

                if (cursor is RecordDecl record)
                {
                    ClangType type = record.TypeForDecl;

                    extra += $" {type.Handle.SizeOf} bytes";

                    if (type.Handle.IsPODType)
                    { extra += " <POD>"; }
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

            string kind = cursor.CursorKindSpellingSafe();
            //kind = cursor.GetType().Name;

            WriteLine($"{cursor.GetType().Name} {kind} {cursor.Handle.DeclKind} - {cursor.Spelling}{extra}");

            // Clang seems to have a basic understanding of Doxygen comments.
            // This seems to associate the comment as appropriate for prefix and postfix documentation. Pretty neat!
#if false
            string commentText = clang.Cursor_getRawCommentText(cursor.Handle).ToString();
            if (!String.IsNullOrEmpty(commentText))
            { WriteLine(commentText); }
#endif

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

            // For defined records, print the layout
            // Helpful: https://github.com/joshpeterson/layout
            bool skipFields = false;
            {
                if (cursor is RecordDecl record && record.Handle.IsDefinition) //TODO: PathogenLayoutExtensions should error on records without a definition.
                {
                    Console.WriteLine($"RECORD: {record.Name}");
                    skipFields = true;
                    bool wroteField = false;

                    foreach (Cursor child in cursor.CursorChildren)
                    {
                        if (child.CursorKind != CXCursorKind.CXCursor_FieldDecl)
                        { continue; }

                        if (!wroteField)
                        {
                            wroteField = true;
                            WriteLine("----------------------------------------------------------------------------");
                        }

                        FieldDecl field = (FieldDecl)child;
                        WriteLine($"{field.Type.AsString} {field.Name} @ {field.Handle.OffsetOfField / 8} for {field.Type.Handle.SizeOf}");
                    }


                    // Dump the layout using PathogenLayoutExtensions
                    WriteLine("----------------------------------------------------------------------------");
                    DumpLayoutWithPathogenExtensions(record);
                    WriteLine("----------------------------------------------------------------------------");
                }
            }

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

                if (skipFields && child is FieldDecl)
                { continue; }

                Dump(child);
            }
            Unindent();
        }

        private static unsafe void DumpLayoutWithPathogenExtensions(RecordDecl record)
        {
            const string PanicMarker = "!!!WARNWARN!!!";
            PathogenRecordLayout* layout = null;

            try
            {
                layout = PathogenExtensions.pathogen_GetRecordLayout(record.Handle);

                if (layout == null)
                {
                    Console.Error.WriteLine($"Failed to get record layout of {record.Name}.");
                    WriteLine($"!!!!!! pathogen_GetRecordLayout Failed !!!!!!");
                    return;
                }

                // Count the number of fields
                int fieldCount = 0;
                for (PathogenRecordField* field = layout->FirstField; field != null; field = field->NextField)
                { fieldCount++; }

                WriteLine($"          Field count: {fieldCount}");
                WriteLine($"                 Size: {layout->Size} bytes");
                WriteLine($"            Alignment: {layout->Alignment} bytes");
                WriteLine($"        Is C++ record: {(layout->IsCppRecord != 0 ? "Yes" : "No")}");

                if (layout->IsCppRecord != 0)
                {
                    WriteLine($"     Non-virtual size: {layout->NonVirtualSize}");
                    WriteLine($"Non-virtual alignment: {layout->NonVirtualAlignment}");
                }

                for (PathogenRecordField* field = layout->FirstField; field != null; field = field->NextField)
                {
                    string fieldLine = $"[{field->Offset}]";

                    if (field->Kind != PathogenRecordFieldKind.Normal)
                    { fieldLine += $" {field->Kind}"; }

                    fieldLine += $" {field->Type} {field->Name.CString}";

#if false
                    if (field->Kind == PathogenRecordFieldKind.Normal)
                    { fieldLine += $" (FieldDeclaration = {field->FieldDeclaration})"; }
#endif

                    if (field->IsPrimaryBase != 0)
                    { fieldLine += " (PRIMARY)"; }

                    switch (field->Kind)
                    {
                        case PathogenRecordFieldKind.VirtualBase:
                        case PathogenRecordFieldKind.VirtualBaseTablePtr:
                        case PathogenRecordFieldKind.VTorDisp:
                            fieldLine += $" {PanicMarker}";
                            break;
                    }

                    WriteLine(fieldLine);
                }

                // Write out the VTable(s)
                int vTableIndex = 0;
                for (PathogenVTable* vTable = layout->FirstVTable; vTable != null; vTable = vTable->NextVTable)
                {
                    WriteLine($"------- VTABLE {vTableIndex} -------");

                    if (vTableIndex > 0)
                    { WriteLine(PanicMarker); }

                    int i = 0;
                    foreach (PathogenVTableEntry entry in vTable->Entries)
                    {
                        string line = $"[{i}] {entry.Kind}";

                        switch (entry.Kind)
                        {
                            case PathogenVTableEntryKind.VCallOffset:
                            case PathogenVTableEntryKind.VBaseOffset:
                            case PathogenVTableEntryKind.OffsetToTop:
                                line += $" {entry.Offset}";
                                break;
                            case PathogenVTableEntryKind.RTTI:
                                line += $" {entry.RttiType.DisplayName}";
                                break;
                            case PathogenVTableEntryKind.FunctionPointer:
                            case PathogenVTableEntryKind.CompleteDestructorPointer:
                            case PathogenVTableEntryKind.DeletingDestructorPointer:
                            case PathogenVTableEntryKind.UnusedFunctionPointer:
                                line += $" {entry.MethodDeclaration.DisplayName}";
                                break;
                        }

                        WriteLine(line);
                        i++;
                    }

                    vTableIndex++;
                }
            }
            finally
            {
                if (layout != null)
                { PathogenExtensions.pathogen_DeleteRecordLayout(layout); }
            }
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
