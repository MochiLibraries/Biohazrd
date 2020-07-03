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
        public bool IsConst => Function is CXXMethodDecl method && method.IsConst;
        public bool IsOperatorOverload { get; }

        public override string DefaultName { get; }

        // When this function is an instance method, we add "Interop" to the end of the P/Invoke methods to ensure they don't conflict with other methods.
        // (For instance, when there's a SomeClass::Method() method in addition to a SomeClass::Method(SomeClass*) method.)
        private string DllImportName => IsInstanceMethod ? $"{TranslatedName}_PInvoke" : TranslatedName;

        private string ThisTypeSanitized => Record is null ? "void" : SanitizeIdentifier(Record.TranslatedName);

        public override bool CanBeRoot => false;

        public bool HideFromIntellisense { get; set; } = false;

        private readonly bool ReturnValueMustBePassedByReference = false;
        private readonly bool HasReturnValue;

        internal TranslatedFunction(IDeclarationContainer container, FunctionDecl function)
            : base(container)
        {
            Function = function;
            Declaration = Function;
            DefaultName = Function.Name;
            Accessibility = AccessModifier.Public;
            ReturnValueMustBePassedByReference = Function.ReturnType.MustBePassedByReference();
            HasReturnValue = Function.ReturnType.Kind != CXTypeKind.CXType_Void;

            // Handle non-public methods
            if (Function is CXXMethodDecl && Function.Access != CX_CXXAccessSpecifier.CX_CXXPublic)
            { Accessibility = AccessModifier.Private; }

            // Handle operator overloads
            ref PathogenOperatorOverloadInfo operatorOverloadInfo = ref Function.GetOperatorOverloadInfo();

            if (operatorOverloadInfo.Kind != PathogenOperatorOverloadKind.None)
            {
                DefaultName = $"operator_{operatorOverloadInfo.Name}";
                IsOperatorOverload = true;
            }

            //TODO: Name these based on the type being converted to/from (Need a way to escape types for identifiers)
            if (Function is CXXConversionDecl)
            {
                DefaultName = Parent.GetNameForUnnamed("ConversionOperator");
                IsOperatorOverload = true;
            }

            // Rename constructors/destructors
            if (Function is CXXConstructorDecl)
            { DefaultName = "Constructor"; }
            else if (Function is CXXDestructorDecl)
            { DefaultName = "Destructor"; }

            // Get the function's calling convention
            string errorMessage;
            CXCallingConv clangCallingConvention = Function.GetCallingConvention();
            CallingConvention = clangCallingConvention.GetCSharpCallingConvention(out errorMessage);

            if (errorMessage is object)
            { File.Diagnostic(Severity.Error, Function, errorMessage); }
        }

        private void WriteReturnType(CodeWriter writer)
            => File.WriteType(writer, Function.ReturnType, Function, TypeTranslationContext.ForReturn);

        private const string DummyThisNameSanitized = "@this";
        private const string DummyReturnBufferNameSanitized = "__returnBuffer";

        private bool FirstParameterListWrite = true;
        private void WriteParameterList(CodeWriter writer, bool rawSignature, bool forArgumentList = false)
        {
            bool first = true;

            if (rawSignature)
            {
                // Write out the this pointer
                if (IsInstanceMethod)
                {
                    if (!forArgumentList)
                    { writer.Write($"{ThisTypeSanitized}* "); }

                    writer.Write(DummyThisNameSanitized);
                    first = false;
                }

                // Write out the return buffer parameter
                if (ReturnValueMustBePassedByReference)
                {
                    if (!first)
                    { writer.Write(", "); }

                    writer.Write("out ");

                    if (!forArgumentList)
                    {
                        WriteReturnType(writer);
                        writer.Write(" ");
                    }

                    writer.Write(DummyReturnBufferNameSanitized);
                    first = false;
                }
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
            string accessibility = Accessibility.ToCSharpKeyword();

            // If this function is an instance method, translate it as private since this p/invoke will be accessed via a trampoline
            if (IsInstanceMethod)
            { accessibility = "private"; }

            writer.EnsureSeparation();

            // Write out the EditorBrowsableAttribute if appllicable
            if (!IsInstanceMethod)
            { TranslateEditorBrowsableAttribute(writer); }

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
            if (ReturnValueMustBePassedByReference)
            { writer.Write("void"); }
            else
            { WriteReturnType(writer); }
            writer.Write($" {SanitizeIdentifier(DllImportName)}(");
            WriteParameterList(writer, rawSignature: true);
            writer.WriteLine(");");
        }

        private void TranslateTrampoline(CodeWriter writer)
        {
            writer.EnsureSeparation();

            // Build the dispatch's method access
            // (We do this now so we can obsolete the method and skip fixing this if there's an issue.)
            string methodAccess = null;
            string methodAccessFailure = null;

            if (!IsVirtual)
            { methodAccess = SanitizeIdentifier(DllImportName); }
            else
            {
                if (Record is null)
                { methodAccessFailure = "Virtual method has no associated class."; }
                else if (Record.VTableField is null)
                { methodAccessFailure = "Class has no vTable pointer."; }
                else if (Record.VTable is null)
                { methodAccessFailure = "Class has no virtual method table."; }
                else
                {
                    string vTableEntry = Record.VTable.GetVTableEntryNameForMethod(this);

                    if (vTableEntry is null)
                    { methodAccessFailure = "Could not find entry in virtual method table."; }
                    else
                    { methodAccess = $"{SanitizeIdentifier(Record.VTableField.TranslatedName)}->{SanitizeIdentifier(vTableEntry)}"; }
                }
            }

            Debug.Assert(methodAccess is object || methodAccessFailure is object, "We should have either vTable access code or an error indicating why we don't.");

            // Obsolete the method if there was an issue building this trampoline
            if (methodAccessFailure is object)
            {
                writer.Using("System");
                writer.WriteLine($"[Obsolete(\"Method not translated: {SanitizeStringLiteral(methodAccessFailure)}\", error: true)]");
                File.Diagnostic(Severity.Warning, Function, $"Can't translate method: {methodAccessFailure}");
            }

            // Write out the EditorBrowsableAttribute if appllicable
            TranslateEditorBrowsableAttribute(writer);

            // Translate the method signature
            writer.Write($"{Accessibility.ToCSharpKeyword()} unsafe ");
            WriteReturnType(writer);
            writer.Write($" {SanitizeIdentifier(TranslatedName)}(");
            WriteParameterList(writer, rawSignature: false);
            writer.WriteLine(")");

            // If we failed to build the method access, just emit an exception
            if (methodAccessFailure is object)
            {
                using (writer.Indent())
                {
                    writer.Using("System");
                    writer.WriteLine($"=> throw new PlatformNotSupportedException(\"Method not available: {SanitizeStringLiteral(methodAccessFailure)}\");");
                }

                return;
            }

            // Translate the dispatch
            using (writer.Block())
            {
                writer.WriteLine($"fixed ({ThisTypeSanitized}* {DummyThisNameSanitized} = &this)");

                if (ReturnValueMustBePassedByReference)
                {
                    Debug.Assert(HasReturnValue);
                    using (writer.Block())
                    {
                        WriteReturnType(writer);
                        writer.WriteLine($" {DummyReturnBufferNameSanitized};");

                        writer.Write($"{methodAccess}(");
                        WriteParameterList(writer, rawSignature: true, forArgumentList: true);
                        writer.WriteLine(");");

                        writer.WriteLine($"return {DummyReturnBufferNameSanitized};");
                    }
                }
                else
                {
                    writer.Write("{ ");

                    if (HasReturnValue)
                    { writer.Write("return "); }

                    writer.Write($"{methodAccess}(");
                    WriteParameterList(writer, rawSignature: true, forArgumentList: true);
                    writer.Write(");");

                    writer.WriteLine(" }");
                }
            }
        }

        protected override void TranslateImplementation(CodeWriter writer)
        {
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
            { writer.Write($"{ThisTypeSanitized}*, "); }

            if (ReturnValueMustBePassedByReference)
            {
                Debug.Assert(HasReturnValue, "If the return value is passed by reference, the method must have a return value too.");
                writer.Write($"out ");
                WriteReturnType(writer);
                writer.Write(", ");
            }

            foreach (ParmVarDecl parameter in Function.Parameters)
            {
                File.WriteType(writer, parameter.Type, parameter, TypeTranslationContext.ForParameter);
                writer.Write(", ");
            }

            if (ReturnValueMustBePassedByReference)
            { writer.Write("void"); }
            else
            { WriteReturnType(writer); }

            writer.Write('>');
        }

        private void TranslateEditorBrowsableAttribute(CodeWriter writer)
        {
            if (HideFromIntellisense)
            {
                writer.Using("System.ComponentModel");
                writer.WriteLine("[EditorBrowsable(EditorBrowsableState.Never)]");
            }
        }
    }
}
