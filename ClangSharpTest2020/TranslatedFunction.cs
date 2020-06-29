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

        private string ThisTypeSanatized => Record is null ? "void" : SanitizeIdentifier(Record.TranslatedName);

        public override bool CanBeRoot => false;

        public bool HideFromIntellisense { get; set; } = false;

        internal TranslatedFunction(IDeclarationContainer container, FunctionDecl function)
            : base(container)
        {
            Function = function;
            Declaration = Function;
            DefaultName = Function.Name;
            Accessibility = AccessModifier.Public;

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
            WriteReturnType(writer);
            writer.Write($" {SanitizeIdentifier(DllImportName)}(");
            WriteParameterList(writer, includeThis: true);
            writer.WriteLine(");");
        }

        private void TranslateTrampoline(CodeWriter writer)
        {
            writer.EnsureSeparation();

            // Write out the EditorBrowsableAttribute if appllicable
            TranslateEditorBrowsableAttribute(writer);

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
                    { vTableAccessFailure = "Could not find entry in virtual method table."; }
                    else
                    { vTableAccess = $"{SanitizeIdentifier(Record.VTableField.TranslatedName)}->{SanitizeIdentifier(vTableEntry)}"; }
                }

                Debug.Assert(vTableAccess is object || vTableAccessFailure is object, "We should have either vTable access code or an error indicating why we don't.");
            }

            // Write out the vTable access couldn't be built
            if (vTableAccessFailure is object)
            {
                writer.Using("System");
                writer.WriteLine($"[Obsolete(\"Method not translated: {SanitizeStringLiteral(vTableAccessFailure)}\", error: true)]");
                File.Diagnostic(Severity.Warning, Function, $"Can't translate virtual method: {vTableAccessFailure}");
            }

            // Translate the method signature
            writer.Write($"{Accessibility.ToCSharpKeyword()} unsafe ");
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
                        writer.WriteLine($"=> throw new PlatformNotSupportedException(\"Virtual method not available: {SanitizeStringLiteral(vTableAccessFailure)}\");");
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
            { writer.Write($"{ThisTypeSanatized}*, "); }

            foreach (ParmVarDecl parameter in Function.Parameters)
            {
                File.WriteType(writer, parameter.Type, parameter, TypeTranslationContext.ForParameter);
                writer.Write(", ");
            }

            WriteReturnType(writer);

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
