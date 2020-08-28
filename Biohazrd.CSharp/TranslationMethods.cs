#if false
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Biohazrd.CSharp
{
    class TranslatedLibrary
    {
        public void Translate(LibraryTranslationMode mode)
        {
            switch (mode)
            {
                case LibraryTranslationMode.OneFilePerType:
                {
                    foreach (TranslatedFile file in Files)
                    { file.Translate(); }
                }
                break;
                case LibraryTranslationMode.OneFilePerInputFile:
                {
                    foreach (TranslatedFile file in Files)
                    {
                        if (file.IsEmptyTranslation)
                        { continue; }

                        using CodeWriter writer = new CodeWriter();
                        file.Translate(writer);
                        string outputFileName = Path.GetFileNameWithoutExtension(file.FilePath) + ".cs";
                        writer.WriteOut(outputFileName);
                    }
                }
                break;
                case LibraryTranslationMode.OneFile:
                {
                    using CodeWriter writer = new CodeWriter();

                    foreach (TranslatedFile file in Files)
                    { file.Translate(writer); }

                    writer.WriteOut("TranslatedLibrary.cs");
                }
                break;
                default:
                    throw new ArgumentException("The specified mode is invalid.", nameof(mode));
            }
        }
    }

    class TranslatedDeclaration
    {
        public void Translate(CodeWriter writer)
        {
            // Dump Clang information
            if (GlobalConfiguration.DumpClangDetails && Declaration is object)
            {
                writer.EnsureSeparation();
                writer.WriteLineLeftAdjusted($"#region {Declaration.CursorKindDetailed()} Dump");

                using (writer.Prefix("// "))
                { ClangSharpInfoDumper.Dump(writer, Declaration); }

                writer.WriteLineLeftAdjusted("#endregion");
                writer.NoSeparationNeededBeforeNextLine();
            }

            TranslateImplementation(writer);
        }

        protected abstract void TranslateImplementation(CodeWriter writer);
    }

    class TranslatedEnum
    {
        protected override void TranslateImplementation(CodeWriter writer)
        {
            if (WillTranslateAsLooseConstants)
            {
                TranslateAsLooseConstants(writer);
                return;
            }

            writer.EnsureSeparation();

            if (IsFlags)
            {
                writer.Using("System");
                writer.WriteLine("[Flags]");
            }

            writer.Write($"{Accessibility.ToCSharpKeyword()} enum ");
            writer.WriteIdentifier(TranslatedName);

            // If the enum has a integer type besides int, emit the base specifier
            if (UnderlyingType != UnderlyingEnumType.Int)
            { writer.Write($" : {UnderlyingType.ToCSharpKeyword()}"); }

            writer.WriteLine();

            using (writer.Block())
            {
                ulong expectedValue = 0;
                bool first = true;
                foreach (EnumConstant value in Values)
                {
                    // If we aren't the first value, write out the comma and newline for the previous value
                    if (first)
                    { first = false; }
                    else
                    { writer.WriteLine(','); }

                    // Determine if we need to write out the value explicitly
                    bool writeOutValue = false;

                    // If the constant has an explicit value in C++, we'll put one in the translation too.
                    //TODO: It'd be nice if we wrote out the expression that created the value into a (doc?) comment. (It's nice for combined enum flags.)
                    if (value.HasExplicitValue)
                    { writeOutValue = true; }
                    // If the value isn't what we expect, write it out explicitly and warn since we don't expect this to happen.
                    else if (value.Value != expectedValue)
                    {
                        writeOutValue = true;
                        File.Diagnostic
                        (
                            Severity.Warning,
                            value.Declaration,
                            $"{EnumDeclaration.Name}.{value.Declaration.Name} had an implicit value, but it had to be translated with an explicit one."
                        );
                    }

                    // Write out the constant name
                    writer.WriteIdentifier(value.Name);

                    if (writeOutValue)
                    {
                        writer.Write(" = ");
                        TranslateConstantValue(writer, value);
                    }

                    // Determine the expected value of the next constant (assuming it's implicit)
                    expectedValue = value.Value + 1;
                }

                // If constants were written, add the newline for the final constant
                if (!first)
                { writer.WriteLine(); }
            }
        }

        private void TranslateAsLooseConstants(CodeWriter writer)
        {
            writer.EnsureSeparation();

            foreach (EnumConstant value in Values)
            {
                writer.Write($"{Accessibility.ToCSharpKeyword()} const {UnderlyingType.ToCSharpKeyword()} ");
                writer.WriteIdentifier(value.Name);
                writer.Write(" = ");
                TranslateConstantValue(writer, value);
                writer.WriteLine(";");
            }
        }

        private void TranslateConstantValue(CodeWriter writer, EnumConstant value)
        {
            // If the constant value is translated as hex, we can just write it out directly
            if (value.IsHexValue)
            {
                // If the value exceeds the maximum value of the underlying type (happens with hex values of signed numbers) we need to add an unchecked explicit cast.
                bool needsCast = value.Value > UnderlyingType.GetMaxValue();
                if (needsCast)
                { writer.Write($"unchecked(({UnderlyingType.ToCSharpKeyword()})"); }

                writer.Write($"0x{value.Value:X}");

                if (needsCast)
                { writer.Write(')'); }
                return;
            }

            // For unsigned values, we can just write out the value directly
            if (!UnderlyingType.IsSigned())
            {
                writer.Write(value.Value);
                return;
            }

            // For signed values, we need to cast the value to the actual signed type
            switch (UnderlyingType)
            {
                case UnderlyingEnumType.SByte:
                    writer.Write((sbyte)value.Value);
                    return;
                case UnderlyingEnumType.Short:
                    writer.Write((short)value.Value);
                    return;
                case UnderlyingEnumType.Int:
                    writer.Write((int)value.Value);
                    return;
                case UnderlyingEnumType.Long:
                    writer.Write((long)value.Value);
                    return;
            }

            // Fallback (we should never get here unless a new underlying enum type is added that we aren't handling.)
            Debug.Assert(false); // Should never get here since it indicates a signed underlying type that we don't support
            writer.Write($"unchecked(({UnderlyingType.ToCSharpKeyword()}){value.Value})");
        }        
    }

    public class TranslatedField
    {
        protected virtual void TranslateType(CodeWriter writer)
        => File.WriteType(writer, FieldType, Context, TypeTranslationContext.ForField);

        protected override void TranslateImplementation(CodeWriter writer)
        {
            writer.Using("System.Runtime.InteropServices");

            writer.EnsureSeparation();
            writer.Write($"[FieldOffset({Offset})] {Accessibility.ToCSharpKeyword()} ");
            TranslateType(writer);
            writer.Write(" ");
            writer.WriteIdentifier(TranslatedName);
            writer.WriteLine(";");
        }
    }

    public class TranslatedFunction
    {
        // When this function is an instance method, we add "Interop" to the end of the P/Invoke methods to ensure they don't conflict with other methods.
        // (For instance, when there's a SomeClass::Method() method in addition to a SomeClass::Method(SomeClass*) method.)
        private string DllImportName => IsInstanceMethod ? $"{TranslatedName}_PInvoke" : TranslatedName;

        private string ThisTypeSanitized => Record is null ? "void" : SanitizeIdentifier(Record.TranslatedName);

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

    class TranslatedNormalField
    {
        protected override void TranslateImplementation(CodeWriter writer)
        {
            //TODO: Bitfields
            using var _bitfields = writer.DisableScope(IsBitField, File, Context, "Unimplemented translation: Bitfields.");

            // If the field is a constant array, we need special translation handling
            ClangType reducedType;
            int levelsOfIndirection;
            File.ReduceType(FieldType, Field, TypeTranslationContext.ForField, out reducedType, out levelsOfIndirection);

            bool isPointerToConstantArray = reducedType.Kind == CXTypeKind.CXType_ConstantArray && levelsOfIndirection > 0;
            using var _pointerToConstantArray = writer.DisableScope(isPointerToConstantArray, File, Context, "Unimplemented translation: Pointer to constant array.");

            if (reducedType is ConstantArrayType constantArray && levelsOfIndirection == 0)
            {
                TranslateConstantArrayField(writer, constantArray);
                return;
            }

            // Perform the translation
            base.TranslateImplementation(writer);
        }

        private void TranslateConstantArrayField(CodeWriter writer, ConstantArrayType constantArrayType)
        {
            using var _constantArrays = writer.DisableScope(true, File, Context, "Disabled translation: Constant array translation needs rethinking.");

            // Reduce the element type
            ClangType reducedElementType;
            int levelsOfIndirection;
            File.ReduceType(constantArrayType.ElementType, Field, TypeTranslationContext.ForField, out reducedElementType, out levelsOfIndirection);

            using var _constantArrayOfArrays = writer.DisableScope(reducedElementType.Kind == CXTypeKind.CXType_ConstantArray, File, Context, "Unimplemented translation: Constant array of constant arrays.");

            // Write out the first element field
            writer.Using("System"); // For ReadOnlySpan<T>
            writer.Using("System.Runtime.InteropServices"); // For FieldOffsetAttribute
            writer.Using("System.Runtime.CompilerServices"); // For Unsafe

            writer.Write($"[FieldOffset({Offset})] private ");
            File.WriteReducedType(writer, reducedElementType, levelsOfIndirection, FieldType, Field, TypeTranslationContext.ForField);
            writer.Write(' ');
            string element0Name = $"__{TranslatedName}_Element0";
            writer.WriteIdentifier(element0Name);
            writer.WriteLine(';');

            writer.Write($"{Accessibility.ToCSharpKeyword()} ReadOnlySpan<");
            File.WriteReducedType(writer, reducedElementType, levelsOfIndirection, FieldType, Field, TypeTranslationContext.ForField);
            writer.Write("> ");
            writer.WriteIdentifier(TranslatedName);
            writer.WriteLine();
            using (writer.Indent())
            {
                writer.Write("=> new ReadOnlySpan<");
                File.WriteReducedType(writer, reducedElementType, levelsOfIndirection, FieldType, Field, TypeTranslationContext.ForField);
                // Note that using fixed would not be valid here since the span leaves the scope where we are fixed.
                // This relies on the fact that TranslatedRecord writes structs out as ref structs. If that were to change, a different strategy is needed here.
                writer.Write(">(Unsafe.AsPointer(ref ");
                writer.WriteIdentifier(element0Name);
                writer.WriteLine($"), {constantArrayType.Size});");
            }
        }
    }

    class TranslatedRecord
    {
        protected override void TranslateImplementation(CodeWriter writer)
        {
            writer.Using("System.Runtime.InteropServices");

            writer.EnsureSeparation();
            //TODO: Documentation comment
            writer.WriteLine($"[StructLayout(LayoutKind.Explicit, Size = {Size})]");
            // Records are translated as ref structs to prevent storing them on the managed heap.
            //TODO: Translating as a ref struct means we can't use the struct in spans, so we probably can't do this. Probably want an analyzer instead.
            // If we decide to support normal structs later on, the following uses of Unsafe become invalid:
            // * TranslatedNormalField.TranslateConstantArrayField
            writer.WriteLine($"{Accessibility.ToCSharpKeyword()} unsafe partial struct {SanitizeIdentifier(TranslatedName)}");
            using (writer.Block())
            {
                // Write out members
                foreach (TranslatedDeclaration member in _Members)
                { member.Translate(writer); }
            }
        }
    }

    class TranslatedStaticField
    {
        private void TranslateType(CodeWriter writer)
        {
            File.WriteType(writer, VariableDeclaration.Type, VariableDeclaration, TypeTranslationContext.ForField);
            writer.Write("*");
        }

        protected override void TranslateImplementation(CodeWriter writer)
        {
            writer.Using("System.Runtime.InteropServices"); // For NativeLibrary
            writer.EnsureSeparation();

            writer.Write($"{Accessibility.ToCSharpKeyword()} static readonly ");
            TranslateType(writer);
            writer.Write(" ");
            writer.WriteIdentifier(TranslatedName);
            writer.Write(" = (");
            TranslateType(writer);
            //TODO: This leaks handles to the native library.
            writer.WriteLine($")NativeLibrary.GetExport(NativeLibrary.Load(\"TODO.dll\"), \"{CodeWriter.SanitizeStringLiteral(VariableDeclaration.Handle.Mangling.ToString())}\");");
        }
    }

    class TranslatedTypedef
    {
        protected override void TranslateImplementation(CodeWriter writer)
        {
            if (GlobalConfiguration.DumpClangDetails)
            {
                writer.Write("// ");

                if (!(Parent is TranslatedFile))
                { writer.Write($"{Accessibility.ToCSharpKeyword()} "); }

                writer.WriteLine($"typedef '{Typedef.UnderlyingType}' '{this}'");
            }
        }
    }

    class TranslatedUndefinedRecord
    {
        protected override void TranslateImplementation(CodeWriter writer)
        {
            // Eventually it'd be nice to mark this with a special attribute and have an analyzer that prevents dereferencing this type.
            writer.EnsureSeparation();
            writer.WriteLine("/// <remarks>This type was forward-declared but never defined. Do not dereference.</remarks>");
            writer.WriteLine($"{Accessibility.ToCSharpKeyword()} ref partial struct {CodeWriter.SanitizeIdentifier(TranslatedName)}");
            using (writer.Block())
            {
            }
        }
    }

    class TranslatedVTable
    {
        /// <summary>Returns the name of the vtable entry for the specified method or null if the method wasn't found.</summary>
        internal string GetVTableEntryNameForMethod(TranslatedFunction function)
        {
            foreach (VTableEntry entry in Entries)
            {
                if (entry.Info.MethodDeclaration == function.Function.Handle)
                { return entry.Name; }
            }

            return null;
        }

        protected override void TranslateImplementation(CodeWriter writer)
        {
            // Associate VTable entries with translated methods
            // We do this as late as possible to avoid weird behaviors when methods are removed (or maybe even added to) records.
            // Note that we don't bother erroring when a method has no corresponding slot since we assume it will complain when it can't find its slot.
            TranslatedFunction[] methods = new TranslatedFunction[Entries.Length];
            foreach (TranslatedFunction method in Record.Members.OfType<TranslatedFunction>().Where(f => f.IsVirtual))
            {
                // Associate the method
                for (int i = 0; i < Entries.Length; i++)
                {
                    // Only function pointer entries are applicable here
                    if (!Entries[i].Info.Kind.IsFunctionPointerKind())
                    { continue; }

                    // Check if this method matches
                    if (Entries[i].Info.MethodDeclaration == method.Function.Handle)
                    {
                        Debug.Assert(methods[i] is null, "Methods should not associate to the same vtable slot more than once.");
                        methods[i] = method;
                    }
                }
            }

            // Translate the vtable
            writer.EnsureSeparation();
            writer.WriteLine("[StructLayout(LayoutKind.Sequential)]");
            writer.WriteLine($"{Accessibility.ToCSharpKeyword()} unsafe struct {SanitizeIdentifier(TranslatedName)}");
            using (writer.Block())
            {
                bool foundFirstFunctionPointer = false;
                for (int i = 0; i < Entries.Length; i++)
                {
                    // If this entry is a function pointer kind, we can start writing
                    // (VTables have non-vtable stuff like RTTI before the vtable pointer, we don't want to translate those.)
                    if (Entries[i].Info.Kind.IsFunctionPointerKind())
                    { foundFirstFunctionPointer = true; }

                    // If we haven't found a function pointer yet, we don't want to start writing
                    if (!foundFirstFunctionPointer)
                    { continue; }

                    TranslatedFunction associatedMethod = methods[i];

                    // For function pointers, write out the signature of the method as a documentation comment
                    if (associatedMethod is object)
                    { writer.WriteLine($"/// <summary>Virtual method pointer for `{associatedMethod.Function.Handle.DisplayName}`</summary>"); }

                    writer.Write("public ");

                    // Write out the entry's type
                    // If we have an associated method, we write out the function pointer type. Otherwise, the entry is untyped.
                    if (associatedMethod is object)
                    { associatedMethod.TranslateFunctionPointerType(writer); }
                    else
                    { writer.Write("void*"); }

                    // Write out the entry's name
                    writer.WriteLine($" {SanitizeIdentifier(Entries[i].Name)};");
                }
            }
        }
    }

    public class TranslatedVTableField
    {
        protected override void TranslateType(CodeWriter writer)
            => writer.Write($"{CodeWriter.SanitizeIdentifier(TranslatedTypeName)}*");
    }
}
#endif
