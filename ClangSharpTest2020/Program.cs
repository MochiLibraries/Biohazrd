//#define DUMP_MODE
//#define DUMP_LOCATION_INFORMATION
#define DUMP_LOCATION_INFORMATION_VERBOSE
//#define DUMP_RECORD_LAYOUTS
#define USE_FILE_WHITELIST
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
                "PxUnixIntrinsics.h", // Not relevant on Windows

                // The following files include anonyomus unions, which causes TranslatedFile.FindCursor to barf.
                "PxMidphaseDesc.h",
                "PxSolverDefs.h",
            };

            foreach (string includeDir in includeDirs)
            {
                foreach (string headerFile in Directory.EnumerateFiles(includeDir, "*.h", SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileName(headerFile);

#if USE_FILE_WHITELIST
                    if (!whiteListedFiles.Contains(fileName))
                    { continue; }
#endif

                    if (blackListedFiles.Contains(fileName))
                    { continue; }

                    files.Add(headerFile);
                }
            }
#endif

#if DUMP_MODE
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
            const string outputDirectory = "Output";
            if (Directory.Exists(outputDirectory))
            { Directory.Delete(outputDirectory, recursive: true); }

            using WorkingDirectoryScope _ = new WorkingDirectoryScope(outputDirectory);
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
                // Implicit attributes are a bunch of extra noise we don't really need.
                //CXTranslationUnit_Flags.CXTranslationUnit_VisitImplicitAttributes |
                // This seemingly causes included files to not be included
                // But like, at all. The file won't parse if it depends on stuff in the include files.
                //CXTranslationUnit_Flags.CXTranslationUnit_SingleFileParse |
                // I was hoping this would help the previous flag work, but it did not.
                //CXTranslationUnit_Flags.CXTranslationUnit_Incomplete |
                // I was hoping this would help figure out what ranges are included, but it's only really informational.
                // It *can* however be used to determine if a source range is a macro expansion.
                //CXTranslationUnit_Flags.CXTranslationUnit_DetailedPreprocessingRecord |
                0
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

#pragma warning disable CS0649
        private static StreamWriter Writer;
#pragma warning restore CS0649

        private static void Dump(Cursor cursor)
        {
            // Skip cursors which come from included files
            if (!cursor.IsFromMainFile())
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
            }

            string kind = cursor.CursorKind.ToString();
            string declKind = cursor.Handle.DeclKind.ToString();

            const string kindPrefix = "CXCursor_";
            if (kind.StartsWith(kindPrefix))
            { kind = kind.Substring(kindPrefix.Length); }

            const string declKindPrefix = "CX_DeclKind_";
            if (declKind.StartsWith(declKindPrefix))
            { declKind = declKind.Substring(declKindPrefix.Length); }

            if (cursor.CursorKind == CXCursorKind.CXCursor_UnexposedDecl)
            { kind = null; }

            if (cursor.Handle.DeclKind == CX_DeclKind.CX_DeclKind_Invalid)
            { declKind = null; }

            string prefix = $"{cursor.GetType().Name}";

            if (kind != null)
            { prefix += $" {kind}"; }

            if (declKind != null)
            { prefix += $" {declKind}"; }

            WriteLine($"{prefix} - {cursor.Spelling}{extra}");

            // Clang seems to have a basic understanding of Doxygen comments.
            // This seems to associate the comment as appropriate for prefix and postfix documentation. Pretty neat!
#if false
            string commentText = clang.Cursor_getRawCommentText(cursor.Handle).ToString();
            if (!String.IsNullOrEmpty(commentText))
            { WriteLine(commentText); }
#endif

#if DUMP_LOCATION_INFORMATION
            bool isFromMainFileStart = cursor.Extent.Start.IsFromMainFile;
            bool isFromMainFileEnd = cursor.Extent.End.IsFromMainFile;
            bool isFromMainFileStart2 = PathogenExtensions.pathogen_Location_isFromMainFile(cursor.Extent.Start) != 0;
            bool isFromMainFileEnd2 = PathogenExtensions.pathogen_Location_isFromMainFile(cursor.Extent.End) != 0;

            bool whacky = false;
#if false
            if (isFromMainFileStart2 != isFromMainFileStart || isFromMainFileEnd2 != isFromMainFileEnd)
            {
                WriteLine("^^^^^WARNWARN1 PREVIOUS CURSOR IS FROM MAIN FILE ACCORDING TO CLANG BUT NOT PATHOGEN!!!!");
                whacky = true;
            }

            if (isFromMainFileStart != isFromMainFileEnd)
            {
                WriteLine("^^^^^WARNWARN2 PREVIOUS CURSOR START AND ENDS DO NOT MATCH IN MAINFILENESS!!!!");
                whacky = true;
            }
#endif

            if (isFromMainFileStart2 != isFromMainFileEnd2)
            {
                WriteLine("^^^^^WARNWARN3 PREVIOUS CURSOR START AND ENDS DO NOT MATCH IN PATHOGEN MAINFILENESS!!!!");
                whacky = true;
            }

#if DUMP_LOCATION_INFORMATION_VERBOSE
            whacky = true;
#endif

            // For preprocessed entities (only emitted when CXTranslationUnit_DetailedPreprocessingRecord is enabled) we emit source locations
            if (cursor is PreprocessedEntity || !cursor.IsFromMainFile() || whacky)
            {
                Indent();
                WriteLine($" From main file: {cursor.Extent.Start.IsFromMainFile} -- {cursor.Extent.End.IsFromMainFile}");
                WriteLine($"From main file2: {PathogenExtensions.pathogen_Location_isFromMainFile(cursor.Extent.Start) != 0} -- {PathogenExtensions.pathogen_Location_isFromMainFile(cursor.Extent.End) != 0}");
                WriteLine($"  From sys file: {cursor.Extent.Start.IsInSystemHeader} -- {cursor.Extent.End.IsInSystemHeader}");
                WriteLocationDetails("      Expansion", cursor.Extent.Start.GetExpansionLocation, cursor.Extent.End.GetExpansionLocation);
                // Note: This is legacy, it was replaced by the expansion location.
                WriteLocationDetails("  Instantiation", cursor.Extent.Start.GetInstantiationLocation, cursor.Extent.End.GetInstantiationLocation);
                WriteLocationDetails("       Spelling", cursor.Extent.Start.GetSpellingLocation, cursor.Extent.End.GetSpellingLocation);
                WriteLocationDetails("           File", cursor.Extent.Start.GetFileLocation, cursor.Extent.End.GetFileLocation);

                {
                    cursor.Extent.Start.GetPresumedLocation(out CXString startFile, out uint startLine, out uint startColumn);
                    cursor.Extent.End.GetPresumedLocation(out CXString endFile, out uint endLine, out uint endColumn);
                    string startFileName = Path.GetFileName(startFile.ToString());
                    string endFileName = Path.GetFileName(endFile.ToString());
                    WriteLocationDetails("       Presumed", startFileName, startLine, startColumn, 0, endFileName, endLine, endColumn, 0);
                }

                WriteLine("--------------------------------------------------------------");
                Unindent();
            }
#endif

#if DUMP_RECORD_LAYOUTS
            // For defined records, print the layout
            // Helpful: https://github.com/joshpeterson/layout
            {
                if (cursor is RecordDecl record && record.Handle.IsDefinition)
                {
                    // Dump the layout using PathogenLayoutExtensions
                    WriteLine("----------------------------------------------------------------------------");
                    DumpLayoutWithPathogenExtensions(record);
                    WriteLine("----------------------------------------------------------------------------");
                }
            }

            // For typedefs, see if we can print their layout
            // This is nice for printing the layout of specialized templates.
            // Unfortunately the RecordDecl for the specialized template won't have a definition unless it's used
            // in another record. Using it in a function signature is sadly not enough.
            // Not sure if this is because Clang doesn't bother generating the internal data structures required to compute layouts or what.
            if (cursor is TypedefDecl typedef)
            {
                if (typedef.TypeForDecl.CanonicalType is RecordType recordType)
                {
                    if (recordType.Decl is RecordDecl recordTypeDecl)
                    {
                        if (recordTypeDecl.Definition is object)
                        {
#if false
                            WriteLine("----------------------------------------------------------------------------");
                            WriteLine($"=== {recordTypeDecl} definition info ===");
                            WriteLine(recordTypeDecl.Definition.CursorKindDetailed());
                            WriteLine($"Extent: {recordTypeDecl.Definition.Extent}");
                            WriteLine($"Same? {ReferenceEquals(recordTypeDecl, recordTypeDecl.Definition)}");
#endif
                            WriteLine("----------------------------------------------------------------------------");
                            DumpLayoutWithPathogenExtensions(recordTypeDecl);
                            WriteLine("----------------------------------------------------------------------------");
                        }
                        else
                        {
                            WriteLine($"---- Could not dump layout of typedefed type '{recordTypeDecl}' because it has no definition.");
                        }
                    }
                }
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

                    for (int i = 0; i < vTable->EntryCount; i++)
                    {
                        PathogenVTableEntry entry = vTable->Entries[i];
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

        private delegate void GetLocationDetails(out CXFile file, out uint line, out uint column, out uint offset);
        private static void WriteLocationDetails(string prefix, GetLocationDetails getStart, GetLocationDetails getEnd)
        {
            getStart(out CXFile startFile, out uint startLine, out uint startColumn, out uint startOffset);
            getEnd(out CXFile endFile, out uint endLine, out uint endColumn, out uint endOffset);

            string startFileName = Path.GetFileName(startFile.Name.ToString());
            string endFileName = Path.GetFileName(endFile.Name.ToString());

            WriteLocationDetails(prefix, startFileName, startLine, startColumn, startOffset, endFileName, endLine, endColumn, endOffset);
        }

        private static void WriteLocationDetails(string prefix, string startFileName, uint startLine, uint startColumn, uint startOffset, string endFileName, uint endLine, uint endColumn, uint endOffset)
        {
            string line = $"{prefix}: ";

            if (startFileName == endFileName)
            {
                line += startFileName;
                line += startLine == endLine ? $":{startLine}" : $":{startLine}..{endLine}";
                line += startColumn == endColumn ? $":{startColumn}" : $":{startColumn}..{endColumn}";

                if (startOffset != 0 && endOffset != 0)
                { line += startOffset == endOffset ? $"[{startOffset}]" : $"[{startOffset}..{endOffset}]"; }
            }
            else
            { line += $" {startFileName}:{startLine}:{startColumn}[{startOffset}]..{endFileName}:{endLine}:{endColumn}[{endOffset}]"; }

            WriteLine(line);
        }
    }
}
