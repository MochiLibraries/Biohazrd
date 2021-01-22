using Biohazrd.CSharp.Metadata;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Biohazrd.CSharp
{
    partial class CSharpLibraryGenerator
    {
        private ref struct EmitFunctionContext
        {
            public string DllImportName { get; }
            public TypeReference? ThisType { get; }
            public string ThisParameterName => "this";
            public string ReturnBufferParameterName => "__returnBuffer";

            public EmitFunctionContext(VisitorContext context, TranslatedFunction declaration)
            {
                // When this function is an instance method, we add a suffix to the P/Invoke method to ensure they don't conflict with other methods.
                // (For instance, when there's a SomeClass::Method() method in addition to a SomeClass::Method(SomeClass*) method.)
                DllImportName = declaration.IsInstanceMethod ? $"{declaration.Name}_PInvoke" : declaration.Name;

                // ThisType is the type of `this` for instance methods.
                if (!declaration.IsInstanceMethod)
                { ThisType = null; }
                else
                {
                    if (context.ParentDeclaration is TranslatedRecord parentRecord)
                    { ThisType = new PointerTypeReference(new PreResolvedTypeReference(context.MakePrevious(), parentRecord)); }
                    else
                    { ThisType = VoidTypeReference.PointerInstance; }
                }
            }
        }

        protected override void VisitFunction(VisitorContext context, TranslatedFunction declaration)
        {
            EmitFunctionContext emitContext = new(context, declaration);

            // Emit the DllImport
            if (!declaration.IsVirtual)
            { EmitFunctionDllImport(context, emitContext, declaration); }

            // Emit the trampoline
            if (declaration.IsInstanceMethod)
            { EmitFunctionTrampoline(context, emitContext, declaration); }
        }

        private static bool FunctionNeedsCharSetParameter(TranslatedFunction declaration)
        {
            if (declaration.ReturnType.IsCSharpType(CSharpBuiltinType.Char))
            { return true; }

            foreach (TranslatedParameter parameter in declaration.Parameters)
            {
                if (parameter.Type.IsCSharpType(CSharpBuiltinType.Char))
                { return true; }
            }

            return false;
        }

        private void EmitFunctionDllImport(VisitorContext context, EmitFunctionContext emitContext, TranslatedFunction declaration)
        {
            Writer.EnsureSeparation();

            // Hide from Intellisense if applicable
            // (Don't do this if the function is accessed via  trampoline.)
            if (!declaration.IsInstanceMethod)
            { EmitEditorBrowsableAttribute(declaration); }

            // Write out the DllImport attribute
            Writer.Using("System.Runtime.InteropServices");
            Writer.Write($"[DllImport(\"{SanitizeStringLiteral(declaration.DllFileName)}\", CallingConvention = CallingConvention.{declaration.CallingConvention}");

            if (declaration.MangledName != emitContext.DllImportName)
            { Writer.Write($", EntryPoint = \"{SanitizeStringLiteral(declaration.MangledName)}\""); }

            if (FunctionNeedsCharSetParameter(declaration))
            { Writer.Write(", CharSet = CharSet.Unicode"); }

            if (declaration.Metadata.Has<SetLastErrorFunction>())
            { Writer.Write(", SetLastError = true"); }

            Writer.WriteLine(", ExactSpelling = true)]");

            // Write out MarshalAs for boolean returns
            if (declaration.ReturnType.IsCSharpType(CSharpBuiltinType.Bool))
            { Writer.WriteLine("[return: MarshalAs(UnmanagedType.I1)]"); }

            // Write out the function signature
            // Instance methods are accessed via trampoline, so we translate the DllImport as private.
            AccessModifier accessibility = declaration.IsInstanceMethod ? AccessModifier.Private : declaration.Accessibility;
            Writer.Write($"{accessibility.ToCSharpKeyword()} static extern ");

            // If the return value is passed by reference, the return type is the return buffer pointer
            if (declaration.ReturnByReference)
            { WriteTypeAsReference(context, declaration, declaration.ReturnType); }
            else
            { WriteType(context, declaration, declaration.ReturnType); }

            Writer.Write($" {SanitizeIdentifier(emitContext.DllImportName)}(");
            EmitFunctionParameterList(context, emitContext, declaration, EmitParameterListMode.DllImportParameters);
            Writer.WriteLine(");");
        }

        private void EmitFunctionTrampoline(VisitorContext context, EmitFunctionContext emitContext, TranslatedFunction declaration)
        {
            if (emitContext.ThisType is null)
            { throw new ArgumentException("A function trampoline is not valid in this context.", nameof(emitContext)); }

            Writer.EnsureSeparation();

            // Build the dispatch's method access
            // (We do this first so we can change our emit if the method is broken.)
            string? methodAccess = null;
            string? methodAccessFailure = null;
            TypeReference? thisTypeCast = null;

            if (!declaration.IsVirtual)
            { methodAccess = SanitizeIdentifier(emitContext.DllImportName); }
            else
            {
                // Figure out how to access the VTable entry
                if (context.ParentDeclaration is not TranslatedRecord record)
                { methodAccessFailure = $"Virtual method has no associated class."; }
                else if (record.VTableField is null)
                { methodAccessFailure = "Class has no vTable pointer."; }
                else if (record.VTable is null)
                { methodAccessFailure = "Class has no virtual method table."; }
                else
                {
                    TranslatedVTableEntry? vTableEntry = null;

                    foreach (TranslatedVTableEntry entry in record.VTable.Entries)
                    {
                        if (entry.MethodDeclaration == declaration.Declaration)
                        {
                            vTableEntry = entry;
                            break;
                        }
                    }

                    if (vTableEntry is null)
                    { methodAccessFailure = "Could not find entry in virtual method table."; }
                    else
                    {
                        methodAccess = $"{SanitizeIdentifier(record.VTableField.Name)}->{SanitizeIdentifier(vTableEntry.Name)}";

                        // Determine if we need to cast the this pointer
                        // (This happens if a virtual method is lifted from a base type to a child.)
                        if (vTableEntry.Type is FunctionPointerTypeReference vTableFunctionPointer
                            && vTableFunctionPointer.ParameterTypes.Length > 0
                            && vTableFunctionPointer.ParameterTypes[0] is PointerTypeReference { Inner: TypeReference vTableThis } vTableThisPointer
                            && (vTableThis is not TranslatedTypeReference vTableThisTranslated || !ReferenceEquals(vTableThisTranslated.TryResolve(context.Library), context.ParentDeclaration)))
                        { thisTypeCast = vTableThisPointer; }
                    }
                }
            }

            Debug.Assert(methodAccess is not null || methodAccessFailure is not null, "We need either a method access or a method failure.");

            // Hide from Intellisense if applicable
            EmitEditorBrowsableAttribute(declaration);

            // Add method implementation options if applicable
            EmitMethodImplAttribute(declaration);

            // Obsolete the method if we won't be able to build the trampoline
            if (methodAccessFailure is not null)
            {
                Writer.Using("System");
                Writer.WriteLine($"[Obsolete(\"Method not translated: {SanitizeStringLiteral(methodAccessFailure)}\", error: true)]");
                Diagnostics.Add(Severity.Error, $"Method trampoline cannot be emitted: {methodAccessFailure}");
            }

            // Emit the method signature
            Writer.Write($"{declaration.Accessibility.ToCSharpKeyword()} unsafe ");
            WriteType(context, declaration, declaration.ReturnType);
            Writer.Write($" {SanitizeIdentifier(declaration.Name)}(");
            EmitFunctionParameterList(context, emitContext, declaration, EmitParameterListMode.TrampolineParameters);
            Writer.WriteLine(')');

            // If we failed to build the method access, just emit an exception
            if (methodAccessFailure is not null)
            {
                using (Writer.Indent())
                {
                    Writer.Using("System");
                    Writer.WriteLine($"=> throw new PlatformNotSupportedException(\"Method not available: {SanitizeStringLiteral(methodAccessFailure)}\");");
                }

                return;
            }

            // Emit the dispatch
            using (Writer.Block())
            {
                Writer.Write($"fixed (");
                WriteType(context, declaration, emitContext.ThisType);
                Writer.WriteLine($" {SanitizeIdentifier(emitContext.ThisParameterName)} = &this)");

                bool hasReturnValue = declaration.ReturnType is not VoidTypeReference;

                if (hasReturnValue && declaration.ReturnByReference)
                {
                    using (Writer.Block())
                    {
                        WriteType(context, declaration, declaration.ReturnType);
                        Writer.WriteLine($" {SanitizeIdentifier(emitContext.ReturnBufferParameterName)};");

                        Writer.Write($"{methodAccess}(");
                        EmitFunctionParameterList(context, emitContext, declaration, EmitParameterListMode.TrampolineArguments, thisTypeCast);
                        Writer.WriteLine(");");

                        Writer.WriteLine($"return {SanitizeIdentifier(emitContext.ReturnBufferParameterName)};");
                    }
                }
                else
                {
                    Writer.Write("{ ");

                    if (hasReturnValue)
                    { Writer.Write("return "); }

                    Writer.Write($"{methodAccess}(");
                    EmitFunctionParameterList(context, emitContext, declaration, EmitParameterListMode.TrampolineArguments, thisTypeCast);
                    Writer.Write(");");

                    Writer.WriteLine(" }");
                }
            }
        }

        private enum EmitParameterListMode
        {
            DllImportParameters,
            TrampolineParameters,
            TrampolineArguments,
        }

        private void EmitFunctionParameterList(VisitorContext context, EmitFunctionContext emitContext, TranslatedFunction declaration, EmitParameterListMode mode, TypeReference? thisCastType = null)
        {
            if (thisCastType is not null && mode != EmitParameterListMode.TrampolineArguments)
            { throw new ArgumentException("Emitting a this cast is only possible for trampoline arguments.", nameof(thisCastType)); }

            bool first = true;

            bool writeImplicitParameters = mode == EmitParameterListMode.DllImportParameters || mode == EmitParameterListMode.TrampolineArguments;
            bool writeTypes = mode == EmitParameterListMode.DllImportParameters || mode == EmitParameterListMode.TrampolineParameters;
            bool writeDefautValues = mode switch
            {
                EmitParameterListMode.DllImportParameters => !declaration.IsInstanceMethod, // We only emit the defaults on the trampoline.
                EmitParameterListMode.TrampolineParameters => true,
                _ => false
            };

            VisitorContext parameterContext = context.Add(declaration);

            // Write out the this/retbuf parameters
            if (writeImplicitParameters)
            {
                // Write out the this pointer
                if (emitContext.ThisType is not null)
                {
                    if (writeTypes)
                    {
                        WriteType(context, declaration, emitContext.ThisType);
                        Writer.Write(' ');
                    }
                    else if (thisCastType is not null)
                    {
                        Writer.Write('(');
                        WriteType(context, declaration, thisCastType);
                        Writer.Write(')');
                    }

                    Writer.WriteIdentifier(emitContext.ThisParameterName);
                    first = false;
                }

                // Write out the return buffer parameter
                if (declaration.ReturnByReference)
                {
                    if (!first)
                    { Writer.Write(", "); }

                    if (!declaration.IsVirtual)
                    { Writer.Write("out "); }
                    else if (mode == EmitParameterListMode.TrampolineArguments)
                    { Writer.Write("&"); }

                    if (writeTypes)
                    {
                        WriteType(context, declaration, declaration.ReturnType);
                        Writer.Write(' ');
                    }

                    Writer.WriteIdentifier(emitContext.ReturnBufferParameterName);
                    first = false;
                }
            }

            // Write out parameters
            foreach (TranslatedParameter parameter in declaration.Parameters)
            {
                if (first)
                { first = false; }
                else
                { Writer.Write(", "); }

                if (writeTypes)
                {
                    if (parameter.ImplicitlyPassedByReference)
                    { WriteTypeAsReference(parameterContext, parameter, parameter.Type); }
                    else
                    {
                        // Write MarshalAs for booleans at pinvoke boundaries
                        if (mode == EmitParameterListMode.DllImportParameters && parameter.Type.IsCSharpType(CSharpBuiltinType.Bool))
                        { Writer.Write("[MarshalAs(UnmanagedType.I1)] "); }

                        WriteType(parameterContext, parameter, parameter.Type);
                    }

                    Writer.Write(' ');
                }

                Writer.WriteIdentifier(parameter.Name);

                if (writeDefautValues && parameter.DefaultValue is not null)
                { Writer.Write($" = {GetConstantAsString(parameterContext, parameter, parameter.DefaultValue, parameter.Type)}"); }
            }
        }

        private void EmitEditorBrowsableAttribute(TranslatedFunction declaration)
        {
            if (declaration.Metadata.Has<HideDeclarationFromIntellisense>())
            {
                Writer.Using("System.ComponentModel");
                Writer.WriteLine("[EditorBrowsable(EditorBrowsableState.Never)]");
            }
        }

        private void EmitMethodImplAttribute(TranslatedFunction declaration)
        {
            if (!declaration.Metadata.TryGet<TrampolineMethodImplOptions>(out TrampolineMethodImplOptions optionsMetadata))
            { return; }

            MethodImplOptions options = optionsMetadata.Options;

            if (options == 0)
            { return; }

            Writer.Using("System.Runtime.CompilerServices");
            Writer.Write("[MethodImpl(");

            bool first = true;
            foreach (MethodImplOptions option in Enum.GetValues<MethodImplOptions>())
            {
                if ((options & option) == option)
                {
                    if (first)
                    { first = false; }
                    else
                    { Writer.Write(" | "); }

                    Writer.Write($"{nameof(MethodImplOptions)}.{option}");
                    options &= ~option;
                }
            }

            if (first || options != 0)
            {
                if (!first)
                { Writer.Write(" | "); }

                Writer.Write($"({nameof(MethodImplOptions)}){(int)options}");
            }

            Writer.WriteLine(")]");
        }

        protected override void VisitParameter(VisitorContext context, TranslatedParameter declaration)
            => FatalContext(context, declaration);
    }
}
