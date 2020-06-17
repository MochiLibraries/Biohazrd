using ClangSharp;
using ClangSharp.Interop;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace ClangSharpTest2020
{
    public sealed class TranslatedFunction : TranslatedDeclaration
    {
        public TranslatedRecord Record { get; }
        public FunctionDecl Function { get; }
        private CallingConvention CallingConvention { get; }

        public bool IsInstanceMethod => Function is CXXMethodDecl method && !method.IsStatic;
        public bool IsVirtual => Function is CXXMethodDecl method && method.IsVirtual;

        public override string TranslatedName => Function.Name;
        private string DllImportName => TranslatedName;

        private string TranslatedAccessibility => !(Function is CXXMethodDecl) || Function.Access == CX_CXXAccessSpecifier.CX_CXXPublic ? "public" : "private";

        private TranslatedFunction(ImmutableArray<TranslationContext> context, TranslatedFile file, TranslatedRecord record, FunctionDecl function)
            : base(context, file)
        {
            Debug.Assert(record == null || record.File == file, "The record and file must be consistent.");

            Record = record;
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
                    file.Diagnostic(Severity.Error, function, "Could not determine function's calling convention.");
                    break;
                default:
                    CallingConvention = default;
                    file.Diagnostic(Severity.Error, function, $"Function uses unsupported calling convention '{clangCallingConvention}'.");
                    break;
            }
        }

        internal TranslatedFunction(ImmutableArray<TranslationContext> context, TranslatedFile file, FunctionDecl function)
            : this(context, file, record: null, function)
        { }

        internal TranslatedFunction(ImmutableArray<TranslationContext> context, TranslatedRecord record, FunctionDecl function)
            : this(context, record.File, record, function)
        { }

        private void WriteReturnType(CodeWriter writer)
            => File.WriteType(writer, Function.ReturnType, Function, TypeTranslationContext.ForReturn);

        private void WriteParameterList(CodeWriter writer, bool includeThis)
        {
            bool first = true;

            if (includeThis && IsInstanceMethod)
            {
                if (Record is object)
                { writer.Write($"{Record.TranslatedName}* _this"); }
                else
                { writer.Write("void* _this"); }
                first = false;
            }

            foreach (ParmVarDecl parameter in Function.Parameters)
            {
                if (first)
                { first = false; }
                else
                { writer.Write(", "); }

                File.WriteType(writer, parameter.Type, parameter, TypeTranslationContext.ForParameter);
                writer.Write($" {parameter.Name}");
            }
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
            writer.Write($" {DllImportName}(");
            WriteParameterList(writer, includeThis: true);
            writer.WriteLine(");");
        }

        private void TranslateTrampoline(CodeWriter writer)
        {
            // Translate the method signature
            writer.EnsureSeparation();
            writer.Write($"{TranslatedAccessibility} unsafe ");
            WriteReturnType(writer);
            writer.Write($" {TranslatedName}(");
            WriteParameterList(writer, includeThis: false);
            writer.WriteLine(")");

            // Translate the dispatch
            if (IsVirtual)
            {
                using (writer.Indent())
                { writer.WriteLine("=> throw null; //TODO: Virtual dispatch"); }  //TODO
            }
            else
            {
                // Once https://github.com/dotnet/csharplang/issues/1792 is implemented, this fixed statement should be removed.
                using (writer.Block())
                {
                    writer.WriteLine($"fixed ({Record.TranslatedName}* thisP = &this)");
                    writer.WriteLine($"{{ {DllImportName}(thisP); }}");
                }
            }
        }

        public override void Translate(CodeWriter writer)
        {
            //TODO: Decide how to translate constructors/destructors
            if (Function is CXXConstructorDecl)
            {
                writer.EnsureSeparation();
                writer.WriteLine($"//TODO: Translate constructor {Function}");
                File.Diagnostic(Severity.Note, Function, "Constructor was not translated.");
                return;
            }

            if (Function is CXXDestructorDecl)
            {
                writer.EnsureSeparation();
                writer.WriteLine($"//TODO: Translate destructor {Function}");
                File.Diagnostic(Severity.Note, Function, "Destructor was not translated.");
                return;
            }

            // Translate the DllImport
            if (!IsVirtual)
            { TranslateDllImport(writer); }

            // Translate trampoline
            if (IsInstanceMethod)
            { TranslateTrampoline(writer); }
        }
    }
}
