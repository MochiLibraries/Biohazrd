//#define DUMP_MODE
//#define DUMP_LOCATION_INFORMATION
#define DUMP_LOCATION_INFORMATION_VERBOSE
//#define DUMP_LOCATION_FILE_INFORMATION_VERBOSE
//#define DUMP_RECORD_LAYOUTS
#define DUMP_EXTRA_FUNCTION_INFO
#define DUMP_FIELD_TYPE_INFO
//#define USE_FILE_ALLOWLIST
#define BUILD_GENERATED_CODE
using ClangSharp;
using ClangSharp.Interop;
using Microsoft.CodeAnalysis;
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
                //"--target=i386-pc-win32",
            };

            foreach (string includeDir in includeDirs)
            { _clangCommandLineArgs.Add($"-I{includeDir}"); }

            string[] clangCommandLineArgs = _clangCommandLineArgs.ToArray();

            CXIndex index = CXIndex.Create(displayDiagnostics: true);

            List<string> files = new List<string>();

#if true
            files.AddRange(Directory.EnumerateFiles("TestHeaders", "*.h", SearchOption.AllDirectories));
            const string outputDirectory = "Output";
#elif true
            files.Add(@"C:\Scratch\imgui\imgui.h");
            const string outputDirectory = "OutputImgui";
#else
            const string outputDirectory = "OutputPhysX";
            HashSet<string> allowedFiles = new HashSet<string>()
            {
                "PxFoundation.h"
            };

            HashSet<string> skippedFiles = new HashSet<string>()
            {
                "PxUnixIntrinsics.h", // Not relevant on Windows
            };

            foreach (string includeDir in includeDirs)
            {
                foreach (string headerFile in Directory.EnumerateFiles(includeDir, "*.h", SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileName(headerFile);

#if USE_FILE_ALLOWLIST
                    if (!allowedFiles.Contains(fileName))
                    { continue; }
#endif

                    if (skippedFiles.Contains(fileName))
                    { continue; }

                    files.Add(headerFile);
                }
            }
#endif

#if DUMP_MODE
            using var writer = new StreamWriter("Output.txt");
            using var typeInfoWriter = new StreamWriter("Output_TypeInfo.txt");
            Writer = writer;
            TypeInfoWriter = typeInfoWriter;

#if DUMP_FIELD_TYPE_INFO
            TypeInfoWriter.WriteLine("-- Fields are included.");
#endif
#if DUMP_EXTRA_FUNCTION_INFO
            TypeInfoWriter.WriteLine("-- Parameters are included.");
#endif
            foreach (CXTypeKind typeKind in typeof(CXTypeKind).GetEnumValues())
            { TypeKindStatistics[typeKind] = 0; }

            foreach (string file in files)
            {
                WriteLine("==============================================================================");
                WriteLine(file);
                Console.WriteLine(file);
                WriteLine("==============================================================================");
                if (!Dump(index, file, clangCommandLineArgs))
                { return; }
            }

            WriteOutTypeKindStatistics();
#else
            // Ensure all files are absolute paths since we're about to change directories
            for (int i = 0; i < files.Count; i++)
            {
                if (!Path.IsPathRooted(files[i]))
                { files[i] = Path.GetFullPath(files[i]); }
            }

            if (Directory.Exists(outputDirectory))
            {
                foreach (string file in Directory.EnumerateFiles(outputDirectory))
                { File.Delete(file); }
            }

            using WorkingDirectoryScope _ = new WorkingDirectoryScope(outputDirectory);

            // Copy the file to the output directory for easier inspection.
            foreach (string file in files)
            { File.Copy(file, Path.GetFileName(file)); }

            // Create the library
            TranslatedLibraryBuilder libraryBuilder = new TranslatedLibraryBuilder();
            libraryBuilder.AddCommandLineArguments(clangCommandLineArgs);
            libraryBuilder.AddFiles(files);

            using TranslatedLibrary library = libraryBuilder.Create();

            // Perform validation
            Console.WriteLine("==============================================================================");
            Console.WriteLine("Performing pre-translation validation...");
            Console.WriteLine("==============================================================================");
            library.Validate();

            // Apply transformations
            Console.WriteLine("==============================================================================");
            Console.WriteLine("Performing library-specific transformations...");
            Console.WriteLine("==============================================================================");
            library.ApplyTransformation(ConstOverloadRenamer.Factory);
            library.ApplyTransformation(PhysXRemovePaddingFieldsTransformation.Factory);
            library.ApplyTransformation(PhysXEnumTransformation.Factory);
            library.ApplyTransformation(PhysxFlagsEnumTransformation.Factory);
            library.ApplyTransformation(MakeEverythingPublicTransformation.Factory);

            // Emit the translation
            Console.WriteLine("==============================================================================");
            Console.WriteLine("Performing translation...");
            Console.WriteLine("==============================================================================");
            library.Translate(LibraryTranslationMode.OneFilePerInputFile);

            // Build csproj
#if BUILD_GENERATED_CODE
            Console.WriteLine("==============================================================================");
            Console.WriteLine("Building generated C# code...");
            Console.WriteLine("==============================================================================");
            {
                CSharpBuildHelper build = new CSharpBuildHelper();
                foreach (string generatedFile in Directory.EnumerateFiles(".", "*.cs", SearchOption.AllDirectories))
                { build.AddFile(generatedFile); }

                int errorCount = 0;
                int warningCount = 0;

                foreach (Diagnostic diagnostic in build.Compile())
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Hidden)
                    { continue; }

                    switch (diagnostic.Severity)
                    {
                        case DiagnosticSeverity.Warning:
                            warningCount++;
                            break;
                        case DiagnosticSeverity.Error:
                            errorCount++;
                            break;
                    }

                    WriteDiagnostic(diagnostic);
                }

                Console.WriteLine($"========== C# build {(errorCount > 0 ? "failed" : "succeeded")}: {errorCount} error(s), {warningCount} warning(s) ==========");
            }
#endif
#endif
        }

        private static bool Dump(in CXIndex index, string sourceFilePath, string[] clangCommandLineArgs)
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
        private static StreamWriter TypeInfoWriter;
        private static SortedDictionary<CXTypeKind, int> TypeKindStatistics = new SortedDictionary<CXTypeKind, int>();
#pragma warning restore CS0649

        private static void Dump(Cursor cursor)
        {
            string extra = "";
            {
                if (cursor is EnumConstantDecl enumConstant)
                {
                    extra += $" = {enumConstant.InitVal}";
                }

                if (cursor is EnumDecl enumDecl)
                {
                    extra += $" IsScoped={enumDecl.IsScoped} IntegerType=`{enumDecl.IntegerType}`";
                }

                string mangling = cursor.Handle.Mangling.ToString();
                if (!string.IsNullOrEmpty(mangling))
                {
                    extra += $" Mangled={mangling}";
                }

                if (cursor is FunctionDecl function)
                {
                    if (function.IsInlined)
                    { extra += " INLINE"; }

                    extra += $" Type={function.Type.Kind}";

                    if (function.Type is AttributedType attributedType)
                    { extra += $" AttrCallConv={attributedType.Handle.FunctionTypeCallingConv}"; }
                    else if (function.Type is FunctionType functionType)
                    { extra += $" CallConv={functionType.CallConv}"; }
                }

                if (cursor is RecordDecl record)
                {
                    ClangType type = record.TypeForDecl;

                    extra += $" {type.Handle.SizeOf} bytes";

                    if (record.IsCanonicalDecl)
                    { extra += " <CANONICAL>"; }

                    if (record.Definition == record)
                    { extra += " <DEFINITION>"; }
                    else if (record.Definition is null)
                    { extra += " <UNDEFINED>"; }
                    else if (record.Definition is object)
                    { extra += $" Definition={ClangSharpLocationHelper.GetFriendlyLocation(record.Definition)}"; }

                    if (type.Handle.IsPODType)
                    { extra += " <POD>"; }
                }

                if (cursor is IntegerLiteral integerLiteral)
                { extra += $" Value=`{integerLiteral.Value}`"; }

                if (cursor is Attr attribute)
                { extra += $" AttributeKind={attribute.Kind}"; }
            }

            WriteLine($"{cursor.CursorKindDetailed(" ")} - {cursor.Spelling}{extra}");

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

#if DUMP_EXTRA_FUNCTION_INFO
            {
                if (cursor is FunctionDecl function)
                {
                    Indent();
                    WriteLine("----------------------------------------------------------------------------");
                    WriteTypeInfo("Return type: ", function.ReturnType, function);

                    int i = 0;
                    foreach (ParmVarDecl parameter in function.Parameters)
                    {
                        WriteTypeInfo($"Parameter {i}: {parameter.Name} of ", parameter.Type, function);
                        i++;
                    }

                    WriteLine("----------------------------------------------------------------------------");
                    Unindent();
                }
            }
#endif

#if DUMP_FIELD_TYPE_INFO
            {
                if (cursor is FieldDecl field)
                { WriteTypeInfo("^---- Field type: ", field.Type, field); }
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

            static string FormatFileName(CXFile file)
            {
                string fileName = file.Name.ToString();

                if (fileName is null)
                { fileName = "<Null>"; }
                else if (fileName.Length == 0)
                { fileName = "<Empty>"; }
#if !DUMP_LOCATION_FILE_INFORMATION_VERBOSE
                else
                { fileName = Path.GetFileName(fileName); }
#else
                // Add the real path name
                string realPathName = file.TryGetRealPathName().ToString();

                if (realPathName is null)
                { realPathName = "<Null>"; }
                else if (realPathName.Length == 0)
                { realPathName = "<None>"; }

                fileName += $" RealPathName={realPathName}";

                // Add the file's handle
                fileName += $" Handle={file.Handle}";

                // Add the file's unique id
                // In practice, TryGetUniqueId can't actually fail.
                // Looking at the LLVM source, it only returns a failure when the file handle or the CXFileUniqueID pointer is null.
                // As such, this will basically always succeed.
                string uniqueId;
                if (file.TryGetUniqueId(out CXFileUniqueID id))
                {
                    unsafe
                    { uniqueId = $"{id.data[0]}{id.data[1]}{id.data[2]}"; }
                }
                else
                { uniqueId = "<Error>"; }

                fileName += $" UniqueId={uniqueId}";
#endif

                return fileName;
            }

            string startFileName = FormatFileName(startFile);
            string endFileName = FormatFileName(endFile);

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

        private static void WriteTypeInfo(string prefix, ClangType type, Cursor context, int recursionLevel = 0)
        {
            string typeInfo = $"{type.GetType().Name} ({type.Kind}) '{type}' SizeOf={type.Handle.SizeOf}";

            if (type is TagType tagType)
            {
                typeInfo += $" DeclKind={tagType.Decl.CursorKindDetailed()} Decl=`{tagType.Decl}` Location=`{ClangSharpLocationHelper.GetFriendlyLocation(tagType.Decl)}`";
            }

            // Write to main output
            WriteLine($"{prefix}{typeInfo}");

            // Write to type info output
            {
                // Write indent
                for (int i = 0; i < recursionLevel; i++)
                { TypeInfoWriter.Write("  "); }

                // Write out the prefix when we aren't the root type
                // (The prefix for the root type comes from the dump and contains information other than what WriteTypeInfo added.)
                if (recursionLevel > 0 && prefix is object)
                { TypeInfoWriter.Write(prefix); }

                // Write type info
                TypeInfoWriter.Write(typeInfo);

                // Add the location for the context if we have it
                if (context is object)
                {
                    context.Location.GetFileLocation(out CXFile file, out uint line, out _, out _);
                    string shortFileName = Path.GetFileName(file.Name.ToString());
                    TypeInfoWriter.Write($" @ {shortFileName}:{line}");
                }

                // Finish the line
                TypeInfoWriter.WriteLine();
            }

            // Log type kind statistics
            {
                int typeKindCount;

                if (!TypeKindStatistics.TryGetValue(type.Kind, out typeKindCount))
                { typeKindCount = 0; }

                typeKindCount++;
                TypeKindStatistics[type.Kind] = typeKindCount;
            }

            // If we're recurssing excessively, complain and stop
            if (recursionLevel >= 100)
            {
                const string excessiveRecursionWarning = "!!!!!!!!!! Type info output truncated, too much recursion !!!!!!!!!!";
                WriteLine(excessiveRecursionWarning);
                TypeInfoWriter.Write(excessiveRecursionWarning);
                return;
            }

            // Function types are a special case
            if (type is FunctionType functionType)
            {
                Indent();
                WriteTypeInfo("Return type: ", functionType.ReturnType, null, recursionLevel + 1);

                if (type is FunctionProtoType functionProtoType)
                {
                    int i = 0;
                    foreach (ClangType parameterType in functionProtoType.ParamTypes)
                    {
                        WriteTypeInfo($"Parameter {i}: ", parameterType, null, recursionLevel + 1);
                        i++;
                    }
                }
                Unindent();
                return;
            }

            // Check for simple recursive types
            ClangType nextType = type switch
            {
                PointerType pointerType => pointerType.PointeeType,
                ReferenceType referenceType => referenceType.PointeeType,
                ArrayType arrayType => arrayType.ElementType,
                AttributedType attributedType => attributedType.ModifiedType,
                ElaboratedType elaboratedType => elaboratedType.NamedType,
                TypedefType typedefType => typedefType.CanonicalType,
                _ => null
            };

            if (nextType is null)
            { return; }

            // Guard against infinite recursion
            if (ReferenceEquals(type, nextType))
            {
                const string wouldBeInfiniteRecursionWarning = "!!!!!!!!!! The previous type would recurse into its self, skipped recursion !!!!!!!!!!";
                WriteLine(wouldBeInfiniteRecursionWarning);
                TypeInfoWriter.Write(wouldBeInfiniteRecursionWarning);
                return;
            }

            // Recurse
            Indent();
            WriteTypeInfo(null, nextType, null, recursionLevel + 1);
            Unindent();
        }

        private static void WriteOutTypeKindStatistics()
        {
            int maxTypeKindNameLength = 0;

            foreach (CXTypeKind kind in TypeKindStatistics.Keys)
            {
                int typeKindNameLength = kind.ToString().Length;

                if (maxTypeKindNameLength < typeKindNameLength)
                { maxTypeKindNameLength = typeKindNameLength; }
            }

            TypeInfoWriter.WriteLine("==============================================================================");
            TypeInfoWriter.WriteLine("Type kind statistics (only includes types which were printed above.)");
            TypeInfoWriter.WriteLine("==============================================================================");
            foreach (KeyValuePair<CXTypeKind, int> typeKindStatistic in TypeKindStatistics)
            {
                string typeKindName = typeKindStatistic.Key.ToString();
                TypeInfoWriter.Write(typeKindName);

                for (int i = typeKindName.Length; i < maxTypeKindNameLength; i++)
                { TypeInfoWriter.Write(' '); }

                TypeInfoWriter.WriteLine($"  {typeKindStatistic.Value}");
            }
        }

        private static void WriteDiagnostic(Diagnostic diagnostic)
        {
            TextWriter output;
            ConsoleColor oldForegroundColor = Console.ForegroundColor;
            ConsoleColor oldBackgroundColor = Console.BackgroundColor;

            try
            {
                switch (diagnostic.Severity)
                {
                    case DiagnosticSeverity.Hidden:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        output = Console.Out;
                        break;
                    case DiagnosticSeverity.Info:
                        output = Console.Out;
                        break;
                    case DiagnosticSeverity.Warning:
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        output = Console.Error;
                        break;
                    case DiagnosticSeverity.Error:
                    default:
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        output = Console.Error;
                        break;
                }

                output.WriteLine(diagnostic);
            }
            finally
            {
                Console.BackgroundColor = oldBackgroundColor;
                Console.ForegroundColor = oldForegroundColor;
            }
        }
    }
}
