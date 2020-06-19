using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static ClangSharpTest2020.CodeWriter;

namespace ClangSharpTest2020
{
    public sealed class TranslatedFunction : TranslatedDeclaration
    {
        public FunctionDecl Function { get; }
        private CallingConvention CallingConvention { get; }

        public TranslatedRecord Record => Parent as TranslatedRecord;

        public bool IsInstanceMethod => Function is CXXMethodDecl method && !method.IsStatic;
        public bool IsVirtual => Function is CXXMethodDecl method && method.IsVirtual;

        public override string TranslatedName => Function.Name;
        private string DllImportName => TranslatedName;

        private string ThisTypeSanatized => Record is null ? "void" : SanitizeIdentifier(Record.TranslatedName);

        public override bool CanBeRoot => false;

        private string TranslatedAccessibility => !(Function is CXXMethodDecl) || Function.Access == CX_CXXAccessSpecifier.CX_CXXPublic ? "public" : "private";

        internal TranslatedFunction(IDeclarationContainer container, FunctionDecl function)
            : base(container)
        {
            Function = function;

            // Determine the calling convention of the function
            // https://github.com/llvm/llvm-project/blob/91801a7c34d08931498304d93fd718aeeff2cbc7/clang/include/clang/Basic/Specifiers.h#L269-L289
            // https://clang.llvm.org/docs/AttributeReference.html#calling-conventions
            // We generally expect this to always be cdecl on x64. (Clang supports some special calling conventions on x64, but C# doesn't support them.)
            CXCallingConv clangCallingConvention = Function.GetCallingConvention();

            switch (clangCallingConvention)
            {
                case CXCallingConv.CXCallingConv_C:
                    CallingConvention = CallingConvention.Cdecl;
                    break;
                case CXCallingConv.CXCallingConv_X86StdCall:
                    CallingConvention = CallingConvention.StdCall;
                    break;
                case CXCallingConv.CXCallingConv_X86FastCall:
                    CallingConvention = CallingConvention.FastCall;
                    break;
                case CXCallingConv.CXCallingConv_X86ThisCall:
                    CallingConvention = CallingConvention.ThisCall;
                    break;
                case CXCallingConv.CXCallingConv_Win64:
                    CallingConvention = CallingConvention.Winapi;
                    break;
                case CXCallingConv.CXCallingConv_Invalid:
                    CallingConvention = default;
                    File.Diagnostic(Severity.Error, function, "Could not determine function's calling convention.");
                    break;
                default:
                    CallingConvention = default;
                    File.Diagnostic(Severity.Error, function, $"Function uses unsupported calling convention '{clangCallingConvention}'.");
                    break;
            }
        }

        private void WriteReturnType(CodeWriter writer)
            => File.WriteType(writer, Function.ReturnType, Function, TypeTranslationContext.ForReturn);

        private bool FirstParameterListWrite = true;
        private const string DummyThisNameSanatized = "@this";
        private void WriteParameterList(CodeWriter writer, bool includeThis, bool forArgumentList = false)
        {
            bool first = true;

            if (includeThis && IsInstanceMethod)
            {
                if (!forArgumentList)
                { writer.Write($"{ThisTypeSanatized}* "); }

                writer.Write(DummyThisNameSanatized);
                first = false;
            }

            int parameterNumber = 0;
            foreach (ParmVarDecl parameter in Function.Parameters)
            {
                if (first)
                { first = false; }
                else
                { writer.Write(", "); }

                if (!forArgumentList)
                {
                    File.WriteType(writer, parameter.Type, parameter, TypeTranslationContext.ForParameter);
                    writer.Write(' ');
                }

                if (String.IsNullOrEmpty(parameter.Name))
                {
                    writer.WriteIdentifier($"__unnamed{parameterNumber}");

                    if (FirstParameterListWrite)
                    { File.Diagnostic(Severity.Note, parameter, $"Parameter {parameterNumber} of {Function} does not have a name."); }
                }
                else
                { writer.WriteIdentifier(parameter.Name); }

                parameterNumber++;
            }

            FirstParameterListWrite = false;
        }

        private void TranslateDllImport(CodeWriter writer)
        {
            string accessibility = TranslatedAccessibility;

            // If this function is an instance method, translate it as private since this p/invoke will be accessed via a trampoline
            if (IsInstanceMethod)
            { accessibility = "private"; }

            writer.EnsureSeparation();

            // Write out the DllImport attribute
            writer.Using("System.Runtime.InteropServices");

            //TODO: Put the DLL name in a constant or something.
            // (In the case of PhysX, some functions come from different libraries, so we need a way to categorize these somehow...)
            writer.Write($"[DllImport(\"TODO.dll\", CallingConvention = CallingConvention.{CallingConvention}");
            
            string mangledName = Function.Handle.Mangling.ToString();
            if (mangledName != DllImportName)
            { writer.Write($", EntryPoint = \"{mangledName}\""); }

            writer.WriteLine(", ExactSpelling = true)]");

            // Write out the function signature
            writer.Write($"{accessibility} static extern ");
            WriteReturnType(writer);
            writer.Write($" {SanitizeIdentifier(DllImportName)}(");
            WriteParameterList(writer, includeThis: true);
            writer.WriteLine(");");
        }

        private void TranslateTrampoline(CodeWriter writer)
        {
            // Build vTable access
            // (We do this now so we can obsolete the method if we can't get it.)
            string vTableAccess = null;
            string vTableAccessFailure = null;

            if (IsVirtual)
            {
                if (Record is null)
                { vTableAccessFailure = "Virtual method has no associated class."; }
                else if (Record.VTableField is null)
                { vTableAccessFailure = "Class has no vTable pointer."; }
                else if (Record.VTable is null)
                { vTableAccessFailure = "Class has no virtual method table."; }
                else
                {
                    string vTableEntry = Record.VTable.GetVTableEntryNameForMethod(this);

                    if (vTableEntry is null)
                    { }
                    else
                    { vTableAccess = $"{SanitizeIdentifier(Record.VTableField.TranslatedName)}->{SanitizeIdentifier(vTableEntry)}"; }
                }

                Debug.Assert(vTableAccess is object || vTableAccessFailure is object, "We should have either vTable access code or an error indicating why we don't.");
            }

            // Write out the vTable access couldn't be built
            if (vTableAccessFailure is object)
            {
                writer.Using("System");
                writer.Write($"[Obsolete(\"Method not translated: {SanitizeStringLiteral(vTableAccessFailure)}\", error: true)]");
                File.Diagnostic(Severity.Warning, Function, $"Can't translate virtual method: {vTableAccessFailure}");
            }

            // Translate the method signature
            writer.EnsureSeparation();
            writer.Write($"{TranslatedAccessibility} unsafe ");
            WriteReturnType(writer);
            writer.Write($" {SanitizeIdentifier(TranslatedName)}(");
            WriteParameterList(writer, includeThis: false);
            writer.WriteLine(")");

            // Translate the dispatch
            if (IsVirtual)
            {
                if (vTableAccessFailure is object)
                {
                    using (writer.Indent())
                    {
                        writer.Using("System");
                        writer.WriteLine($"=> new PlatformNotSupportedException(\"Virtual method not available: {SanitizeStringLiteral(vTableAccessFailure)}\");");
                    }
                }
                else
                {
                    using (writer.Block())
                    {
                        writer.WriteLine($"fixed ({ThisTypeSanatized}* {DummyThisNameSanatized} = &this)");
                        writer.Write("{ ");

                        if (Function.ReturnType.Kind != CXTypeKind.CXType_Void)
                        { writer.Write("return "); }

                        writer.Write($"{vTableAccess}(");
                        WriteParameterList(writer, includeThis: true, forArgumentList: true);
                        writer.WriteLine("); }");
                    }
                }
            }
            else
            {
                // Once https://github.com/dotnet/csharplang/issues/1792 is implemented, this fixed statement should be removed.
                using (writer.Block())
                {
                    writer.WriteLine($"fixed ({ThisTypeSanatized}* {DummyThisNameSanatized} = &this)");
                    writer.Write("{ ");

                    if (Function.ReturnType.Kind != CXTypeKind.CXType_Void)
                    { writer.Write("return "); }

                    writer.Write($"{SanitizeIdentifier(DllImportName)}(");
                    WriteParameterList(writer, includeThis: true, forArgumentList: true);
                    writer.WriteLine("); }");
                }
            }
        }

        public override void Translate(CodeWriter writer)
        {
            //TODO: Implement these translations
            using var _0 = writer.DisableScope(Function is CXXConstructorDecl, File, Function, "Unimplemented translation: Constructor");
            using var _1 = writer.DisableScope(Function is CXXDestructorDecl, File, Function, "Unimplemented translation: Destructor");
            using var _2 = writer.DisableScope(Function.Name.StartsWith("operator"), File, Function, "Unimplemented translation: Operator overload");

            //TODO: Currently this happens for inline method bodies declared outside of the record. Need to figure out how to ignore/reassociate them.
            // We probably want to try and re-associate them because sometimes the definition has the parameter names but the declaration doesn't.
            using var _3 = writer.DisableScope(IsInstanceMethod && Record is null, File, Function, "Translation hole: Instance method without an associated record.");

            // Translate the DllImport
            if (!IsVirtual)
            { TranslateDllImport(writer); }

            // Translate trampoline
            if (IsInstanceMethod)
            { TranslateTrampoline(writer); }
        }

        public void TranslateFunctionPointerType(CodeWriter writer)
        {
            writer.Write($"delegate* {CallingConvention.ToString().ToLowerInvariant()}<");

            if (IsInstanceMethod)
            { writer.Write($"{ThisTypeSanatized}*, "); }

            foreach (ParmVarDecl parameter in Function.Parameters)
            {
                File.WriteType(writer, parameter.Type, parameter, TypeTranslationContext.ForParameter);
                writer.Write(", ");
            }

            WriteReturnType(writer);

            writer.Write('>');
        }
    }
}
