using Biohazrd.OutputGeneration;
using Biohazrd.Transformation;
using ClangSharp;
using ClangSharp.Interop;
using ClangSharp.Pathogen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Biohazrd.Utilities
{
    public sealed class InlineExportHelper : TransformationBase
    {
        protected override bool SupportsConcurrency => false;
        private bool Used = false;
        private readonly List<TranslatedFunction> FunctionsExportedViaFunctionPointer = new();
        private readonly List<TranslatedFunction> FunctionsExportedViaTrampoline = new();
        private volatile int NextTrampolineId = 0;
        private readonly CppCodeWriter Writer;

        public bool __ItaniumExportMode = false;

        public InlineExportHelper(OutputSession session, string filePath)
            => Writer = session.Open<CppCodeWriter>(filePath);

        public void ForceInclude(string filePath, bool systemInclude = false)
        {
            if (Used)
            { throw new InvalidOperationException("This transformation has already been applied, no more includes can be added."); }

            Writer.Include(filePath, systemInclude);
        }

        protected override TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
        {
            if (Used)
            { throw new InvalidOperationException("This transformation can only be applied once."); }

            // Ensure that the include files written to the output are in the same order as they were included to build the library
            // (Some libraries are sensitive to the order their files are included.)
            Dictionary<string, int> fileIndices = new(library.Files.Length);
            foreach (TranslatedFile file in library.Files)
            {
                Debug.Assert(Path.IsPathFullyQualified(file.FilePath)); // SetIncludeComparer uses fully-qualified paths. TranslatedFile is expected to as well.
                fileIndices.Add(file.FilePath, fileIndices.Count);
            }

            Writer.SetIncludeComparer((a, b) =>
            {
                int ai;
                int bi;

                if (!fileIndices.TryGetValue(a, out ai))
                { ai = Int32.MaxValue; }

                if (!fileIndices.TryGetValue(b, out bi))
                { bi = Int32.MaxValue; }

                if (ai == bi)
                { return StringComparer.InvariantCulture.Compare(a, b); }
                else
                { return ai.CompareTo(bi); }
            });

            return library;
        }

        private TranslatedFunction ExportViaFunctionPointer(TranslatedFunction declaration)
        {
            FunctionsExportedViaFunctionPointer.Add(declaration);
            return declaration;
        }

        private TranslatedFunction ExportViaTrampoline(TranslatedFunction declaration)
        {
            int trampolineId = Interlocked.Increment(ref NextTrampolineId) - 1;
            declaration = declaration with
            {
                MangledName = $"__InlineHelper{trampolineId}",
                CallingConvention = CallingConvention.Cdecl
            };

            FunctionsExportedViaTrampoline.Add(declaration);
            return declaration;
        }

        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            // Edge case: All static non-method functions need to be exported by a trampoline (even the non-inline ones.)
            // They can't be exported at all. These hardly ever appear in a well-formed C++ library's headers.
            if (declaration.Declaration is FunctionDecl { StorageClass: CX_StorageClass.CX_SC_Static } and not CXXMethodDecl)
            { return ExportViaTrampoline(declaration); }

            // Only consider inline functions
            if (!declaration.IsInline)
            { return declaration; }

            // Virtual methods do not need to be exported
            if (declaration.IsVirtual)
            { return declaration; }

            // If the function is not backed by a Clang function, we skip it
            // We don't emit a warning since we assume this function is special and was added intentionally.
            // The fact that a synthesized function is marked as inline is a bit odd, so we won't pretend to know what the intent is.
            if (declaration.Declaration is not FunctionDecl functionDecl)
            { return declaration; }

            // Private/protected methods can't be exported using the techniques we use today
            // https://github.com/InfectedLibraries/Biohazrd/issues/162
            if (functionDecl is CXXMethodDecl methodDecl && methodDecl.Access != CX_CXXAccessSpecifier.CX_CXXPublic)
            {
                return declaration with { Diagnostics = declaration.Diagnostics.Add(Severity.Warning, "Method needs to be exported, but it isn't public.") };
            }

            switch (declaration.SpecialFunctionKind)
            {
                // Normal functions/methods as well as operator/conversion overloads can be exported via function pointer
                case SpecialFunctionKind.None:
                case SpecialFunctionKind.OperatorOverload:
                case SpecialFunctionKind.ConversionOverload:
                    return ExportViaFunctionPointer(declaration);
                // You cannot take a function pointer to a construcor or destructor, so they unfortunately need to be accessed via trampolines
                //TODO: The C++ library can still force the inline constructor to be exported with __declspec(dllexport), ideally we should skip the trampoline for these.
                case SpecialFunctionKind.Constructor:
                {
                    // We can't use a trampoline function to call an abstract constructor, a different approach is necessary here.
                    // https://github.com/InfectedLibraries/Biohazrd/issues/14
                    if (functionDecl.SemanticParentCursor is CXXRecordDecl { IsAbstract: true })
                    {
                        return declaration with { Diagnostics = declaration.Diagnostics.Add(Severity.Warning, "Constructor needs to be exported to be accessible, but it is abstract.") };
                    }

                    return ExportViaTrampoline(declaration);
                }
                case SpecialFunctionKind.Destructor:
                    return ExportViaTrampoline(declaration);
                default:
                    return declaration with
                    {
                        Diagnostics = declaration.Diagnostics.Add(Severity.Warning, $"Not sure how to force export of inline {declaration.SpecialFunctionKind} function.")
                    };
            }
        }

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
        {
            const string dummyNamespaceName = "____BiohazrdInlineExportHelpers";

            //=====================================================================================
            // Handle function pointer exports
            //=====================================================================================
            // Function pointer exports come in two halves:
            // 1) Explicit export of the inline function via an option passed to the linker. (Specified via the linker option pragma)
            // 2) A dummy reference made to the inline function via function pointer
            // 1 is what actually puts the inline function in the DLL, but 2 is required for the linker to be able to export it since
            // it forces the compiler to emit the inline function on its own since that's intrinsically required to take an address to its code.
            // (Without this, the linker will fail to resolve the symbol since the inline function most likely only exists within other functions where it is called.)
            if (FunctionsExportedViaFunctionPointer.Count > 0)
            {
                // Instruct the linker to export the symbols
                if (!__ItaniumExportMode)
                {
                    foreach (TranslatedFunction function in FunctionsExportedViaFunctionPointer)
                    { Writer.WriteLine($"#pragma comment(linker, \"/export:{function.MangledName}\")"); }
                }

                Writer.EnsureSeparation();

                // Add dummy reference to the inline functions so they will exist for the linker
                Writer.WriteLine($"namespace {dummyNamespaceName}");
                using (Writer.Block())
                {
                    if (__ItaniumExportMode)
                    { Writer.WriteLineLeftAdjusted("#pragma GCC visibility push(hidden)"); }

                    for (int i = 0; i < FunctionsExportedViaFunctionPointer.Count; i++)
                    {
                        TranslatedFunction function = FunctionsExportedViaFunctionPointer[i];
                        Debug.Assert(function.Declaration is FunctionDecl);
                        FunctionDecl functionDecl = (FunctionDecl)function.Declaration!;

                        // Include the header containing the function to be referenced
                        if (function.File != TranslatedFile.Synthesized)
                        { Writer.Include(function.File.FilePath); }

                        // Emit the reference
                        // (It'd be nice to use the auto keyword here, but we can't handle overloaded functions if we do.)
                        Writer.Write($"{functionDecl.ReturnType.CanonicalType} (");

                        if (functionDecl is CXXMethodDecl { IsStatic: false })
                        {
                            WriteOutNamespaceAndType(functionDecl);
                            Writer.Write("* ");
                        }
                        else
                        { Writer.Write('*'); }

                        Writer.Write($"unused{i})(");

                        bool first = true;
                        foreach (ParmVarDecl parameter in functionDecl.Parameters)
                        {
                            if (first)
                            { first = false; }
                            else
                            { Writer.Write(", "); }

                            Writer.Write(parameter.Type.CanonicalType.ToString());
                        }

                        Writer.Write(')');

                        if (functionDecl is CXXMethodDecl { IsConst: true })
                        { Writer.Write(" const"); }

                        Writer.Write(" = &");
                        WriteOutNamespaceAndType(functionDecl);
                        Writer.WriteLine($"{functionDecl};");
                    }

                    if (__ItaniumExportMode)
                    { Writer.WriteLineLeftAdjusted("#pragma GCC visibility pop"); }
                }
            }

            //=====================================================================================
            // Handle trampoline exports
            //=====================================================================================
            // Trampoline exports are used for functions which cannot be exported using the technique above (Constructors and destructors in particular.)
            if (FunctionsExportedViaTrampoline.Count > 0)
            {
                const string thisPointerName = "_this";
                const string placementNewHelperName = "__BiohazrdNewHelper";

                // Emit helper operator new to avoid relying on including <new>
                // (Which is problematic on some platforms according to https://github.com/ocornut/imgui/blob/a8f76c23a481162c69e462a52ea7d6f4ade96b32/imgui.h#L1596)
                Writer.EnsureSeparation();
                Writer.WriteLine($"namespace {dummyNamespaceName}");
                using (Writer.Block())
                { Writer.WriteLine($"struct {placementNewHelperName} {{ }};"); }

                // operator new/operator delete cannot be nested within a namespace
                Writer.EnsureSeparation();
                Writer.WriteLine($"inline void* operator new(size_t, {dummyNamespaceName}::{placementNewHelperName}, void* {thisPointerName}) {{ return {thisPointerName}; }}");
                Writer.WriteLine($"inline void operator delete(void*, {dummyNamespaceName}::{placementNewHelperName}, void*) {{ }}");

                Writer.EnsureSeparation();
                Writer.WriteLine("#pragma warning(disable: 4190) // C-linkage function returning C++ type");
                Writer.WriteLine($"extern \"C\" namespace {dummyNamespaceName}");
                using (Writer.Block())
                {
                    //TODO: We should use a call strategy for Itanium instead
                    // See https://github.com/InfectedLibraries/Biohazrd/issues/209
                    if (__ItaniumExportMode)
                    { Writer.WriteLineLeftAdjusted("#pragma GCC visibility push(default)"); }

                    foreach (TranslatedFunction function in FunctionsExportedViaTrampoline)
                    {
                        Debug.Assert(function.Declaration is FunctionDecl);
                        FunctionDecl functionDecl = (FunctionDecl)function.Declaration!;
                        RecordDecl? parentRecord = null;

                        if (functionDecl is CXXMethodDecl)
                        {
                            parentRecord = functionDecl.SemanticParentCursor as RecordDecl;
                            Debug.Assert(parentRecord is not null);
                        }

                        // Include the header containing the function to be referenced
                        if (function.File != TranslatedFile.Synthesized)
                        { Writer.Include(function.File.FilePath); }

                        Writer.EnsureSeparation();
                        if (!__ItaniumExportMode)
                        { Writer.Write("__declspec(dllexport) "); }

                        // Write return type
                        if (functionDecl is CXXConstructorDecl)
                        {
                            // Constructors return a pointer to themselves
                            Writer.Write($"{parentRecord!.TypeForDecl.CanonicalType}*");
                        }
                        else
                        { Writer.Write(functionDecl.ReturnType.CanonicalType.ToString()); }

                        Writer.Write($" {function.MangledName}(");

                        // Write parameter list
                        bool first = true;
                        if (functionDecl is CXXMethodDecl)
                        {
                            Writer.Write($"{parentRecord!.TypeForDecl.CanonicalType}* {thisPointerName}");
                            first = false;
                        }

                        for (int i = 0; i < functionDecl.Parameters.Count; i++)
                        {
                            if (first)
                            { first = false; }
                            else
                            { Writer.Write(", "); }

                            Writer.Write(functionDecl.Parameters[i].Type.CanonicalType.GetSpellingWithPlaceholder($"_{i}"));
                        }

                        Writer.WriteLine(')');
                        Writer.Write("{ ");

                        // Write return statement if necessary
                        if (functionDecl is CXXConstructorDecl || functionDecl.ReturnType.CanonicalType is not BuiltinType { Kind: CXTypeKind.CXType_Void })
                        { Writer.Write("return "); }

                        // Write out function to be called
                        if (functionDecl is CXXConstructorDecl)
                        { Writer.Write($"new ({placementNewHelperName}(), {thisPointerName}) {parentRecord!.TypeForDecl.CanonicalType}"); }
                        else if (functionDecl is CXXMethodDecl)
                        { Writer.Write($"{thisPointerName}->{functionDecl.Name}"); }
                        else
                        { Writer.Write(functionDecl.Name.ToString()); }

                        // Write out argument list
                        Writer.Write('(');

                        for (int i = 0; i < functionDecl.Parameters.Count; i++)
                        {
                            if (i > 0)
                            { Writer.Write(", "); }

                            Writer.Write($"_{i}");
                        }

                        Writer.WriteLine("); }");
                    }

                    if (__ItaniumExportMode)
                    { Writer.WriteLineLeftAdjusted("#pragma GCC visibility pop"); }
                }
            }

            Writer.Finish();
            Writer.Dispose(); // We explicitly dispose early so the file is released and accessible by the native compiler if it is invoked by the generator.
            Used = true;
            return library;
        }

        private void WriteOutNamespaceAndType(Cursor cursor)
        {
            if (cursor is TranslationUnitDecl || cursor.SemanticParentCursor is null)
            { return; }

            WriteOutNamespaceAndType(cursor.SemanticParentCursor);

            switch (cursor)
            {
                case NamespaceDecl namespaceDeclaration:
                    Writer.Write($"{namespaceDeclaration.Name}::");
                    return;
                case RecordDecl recordDeclaration:
                    Writer.Write($"{recordDeclaration.Name}::");
                    return;
            }
        }
    }
}
