using Biohazrd.CSharp.Metadata;
using ClangSharp.Pathogen;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Biohazrd.CSharp
{
    partial class CSharpLibraryGenerator
    {
        private ref struct EmitFunctionContext
        {
            public bool NeedsTrampoline { get; }
            public string DllImportName { get; }
            public TypeReference? ThisType { get; }
            public string ThisParameterName => "this";
            public string ReturnBufferParameterName => "__returnBuffer";

            public EmitFunctionContext(VisitorContext context, TranslatedFunction declaration)
            {
                // We emit a trampoline for functions which are instance methods or return via reference to hide those ABI semantics
                NeedsTrampoline = declaration.IsInstanceMethod || declaration.ReturnByReference;

                // When this function is uses a trampoline, we add a suffix to the P/Invoke method to ensure they don't conflict with other methods.
                // (For instance, when there's a SomeClass::Method() method in addition to a SomeClass::Method(SomeClass*) method.)
                DllImportName = NeedsTrampoline ? $"{declaration.Name}_PInvoke" : declaration.Name;

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
            if (!declaration.IsCallable)
            {
                Fatal(context, declaration, $"{declaration.Name} is missing ABI information and cannot be called.");
                return;
            }

            EmitFunctionContext emitContext = new(context, declaration);

            // Emit the DllImport
            if (!declaration.IsVirtual)
            { EmitFunctionDllImport(context, emitContext, declaration); }

            // Emit the trampoline
            if (emitContext.NeedsTrampoline)
            { EmitFunctionTrampoline(context, emitContext, declaration); }
        }

        private static bool FunctionNeedsCharSetParameter(TranslatedLibrary library, TranslatedFunction declaration)
        {
            if (declaration.ReturnType.IsCSharpType(library, CSharpBuiltinType.Char))
            { return true; }

            foreach (TranslatedParameter parameter in declaration.Parameters)
            {
                if (parameter.Type.IsCSharpType(library, CSharpBuiltinType.Char))
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

            if (FunctionNeedsCharSetParameter(context.Library, declaration))
            { Writer.Write(", CharSet = CharSet.Unicode"); }

            if (declaration.Metadata.Has<SetLastErrorFunction>())
            { Writer.Write(", SetLastError = true"); }

            Writer.WriteLine(", ExactSpelling = true)]");

            // Write out MarshalAs for boolean returns
            if (declaration.ReturnType.IsCSharpType(context.Library, CSharpBuiltinType.Bool))
            { Writer.WriteLine("[return: MarshalAs(UnmanagedType.I1)]"); }

            // Write out the function signature
            // The P/Invokes for functions accessed via trampoline are emitted as private.
            AccessModifier accessibility = emitContext.NeedsTrampoline ? AccessModifier.Private : declaration.Accessibility;
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
            if (!emitContext.NeedsTrampoline)
            { throw new ArgumentException("A function trampoline is not valid in this context.", nameof(emitContext)); }

            Writer.EnsureSeparation();

            // Build the dispatch's method access
            // (We do this first so we can change our emit if the method is broken.)
            string? methodAccess = null;
            string? methodAccessFailure = null;

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
                else if (declaration.Declaration is null)
                { methodAccessFailure = "Virtual method has no associated Clang declaration."; }
                else
                {
                    TranslatedVTableEntry? vTableEntry = null;

                    foreach (TranslatedVTableEntry entry in record.VTable.Entries)
                    {
                        if (entry.Info.MethodDeclaration == declaration.Declaration.Handle)
                        {
                            vTableEntry = entry;
                            break;
                        }
                    }

                    if (vTableEntry is null)
                    { methodAccessFailure = "Could not find entry in virtual method table."; }
                    else
                    { methodAccess = $"{SanitizeIdentifier(record.VTableField.Name)}->{SanitizeIdentifier(vTableEntry.Name)}"; }
                }
            }

            Debug.Assert(methodAccess is not null || methodAccessFailure is not null, "We need either a method access or a method failure.");

            // Hide from Intellisense if applicable
            EmitEditorBrowsableAttribute(declaration);

            // Hide from the debugger if applicable
            if (Options.HideTrampolinesFromDebugger)
            {
                Writer.Using("System.Diagnostics");
                Writer.WriteLine("[DebuggerStepThrough, DebuggerHidden]");
            }

            // Add method implementation options if applicable
            EmitMethodImplAttribute(declaration);

            // Obsolete the method if we won't be able to build the trampoline
            if (methodAccessFailure is not null)
            {
                Writer.Using("System");
                Writer.WriteLine($"[Obsolete(\"Method not translated: {SanitizeStringLiteral(methodAccessFailure)}\", error: true)]");
                Diagnostics.Add(Severity.Error, $"Method trampoline cannot be emitted: {methodAccessFailure}");
            }

            // If this is a constructor, determine if we'll emit it as an actual constructor
            string? constructorName = null;
            if (declaration.SpecialFunctionKind == SpecialFunctionKind.Constructor && context.ParentDeclaration is TranslatedRecord constructorType)
            {
                constructorName = constructorType.Name;

                // Parameterless constructors require C# 10
                if (declaration.Parameters.Length == 0 && Options.TargetLanguageVersion < TargetLanguageVersion.CSharp10)
                { constructorName = null; }
            }

            // Emit the method signature
            if (constructorName is null)
            {
                Writer.Write($"{declaration.Accessibility.ToCSharpKeyword()} ");
                if (!declaration.IsInstanceMethod)
                { Writer.Write("static "); }
                WriteTypeForTrampoline(context, declaration, declaration.ReturnType);
                Writer.Write($" {SanitizeIdentifier(declaration.Name)}(");
                EmitFunctionParameterList(context, emitContext, declaration, EmitParameterListMode.TrampolineParameters);
                Writer.WriteLine(')');
            }
            // Emit the constructor signature
            else
            {
                Writer.Write($"{declaration.Accessibility.ToCSharpKeyword()} ");
                Writer.Write($"{SanitizeIdentifier(constructorName)}(");
                EmitFunctionParameterList(context, emitContext, declaration, EmitParameterListMode.TrampolineParameters);
                Writer.WriteLine(')');
            }

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
                bool hasThis;
                if (emitContext.ThisType is not null)
                {
                    hasThis = true;
                    Debug.Assert(declaration.IsInstanceMethod);

                    Writer.Write($"fixed (");
                    WriteType(context, declaration, emitContext.ThisType!);
                    Writer.WriteLine($" {SanitizeIdentifier(emitContext.ThisParameterName)} = &this)");
                }
                else
                {
                    hasThis = false;
                    Debug.Assert(!declaration.IsInstanceMethod);
                }

                bool hasReturnValue = declaration.ReturnType is not VoidTypeReference;

                if (hasReturnValue && declaration.ReturnByReference)
                {
                    void EmitFunctionBodyWithReturnByReference(EmitFunctionContext emitContext)
                    {
                        WriteType(context, declaration, declaration.ReturnType);
                        Writer.WriteLine($" {SanitizeIdentifier(emitContext.ReturnBufferParameterName)};");

                        Writer.Write($"{methodAccess}(");
                        EmitFunctionParameterList(context, emitContext, declaration, EmitParameterListMode.TrampolineArguments);
                        Writer.WriteLine(");");

                        Writer.WriteLine($"return {SanitizeIdentifier(emitContext.ReturnBufferParameterName)};");
                    }

                    if (hasThis)
                    {
                        // If we have a fixed statement for the this pointer, wrap the return buffer logic with a block
                        using (Writer.Block())
                        { EmitFunctionBodyWithReturnByReference(emitContext); }
                    }
                    else
                    { EmitFunctionBodyWithReturnByReference(emitContext); }
                }
                else
                {
                    // If we have a fixed statement for the this pointer, write out the curly braces for it
                    if (hasThis)
                    { Writer.Write("{ "); }

                    if (hasReturnValue)
                    { Writer.Write("return "); }

                    Writer.Write($"{methodAccess}(");
                    EmitFunctionParameterList(context, emitContext, declaration, EmitParameterListMode.TrampolineArguments);
                    Writer.Write(");");

                    if (hasThis)
                    { Writer.WriteLine(" }"); }
                }
            }
        }

        private void EmitFunctionPointerForVTable(VisitorContext context, EmitFunctionContext emitContext, TranslatedFunction declaration)
        {
            string? callingConventionString = declaration.CallingConvention switch
            {
                CallingConvention.Cdecl => "unmanaged[Cdecl]",
                CallingConvention.StdCall => "unmanaged[Stdcall]",
                CallingConvention.ThisCall => "unmanaged[Thiscall]",
                CallingConvention.FastCall => "unmanaged[Fastcall]",
                _ => null
            };

            if (callingConventionString is null)
            {
                Fatal(context, declaration, $"The {declaration.CallingConvention} convention is not supported.");
                Writer.Write("void*");
                return;
            }

            Writer.Write($"delegate* {callingConventionString}<");

            EmitFunctionParameterList(context, emitContext, declaration, EmitParameterListMode.VTableFunctionPointerParameters);

            Writer.Write(", ");

            if (declaration.ReturnByReference)
            { WriteTypeAsReference(context, declaration, declaration.ReturnType); }
            else
            { WriteType(context, declaration, declaration.ReturnType); }

            Writer.Write('>');
        }

        private enum EmitParameterListMode
        {
            DllImportParameters,
            TrampolineParameters,
            TrampolineArguments,
            VTableFunctionPointerParameters,
        }

        private void EmitFunctionParameterList(VisitorContext context, EmitFunctionContext emitContext, TranslatedFunction declaration, EmitParameterListMode mode)
        {
            if (declaration.FunctionAbi is null)
            { throw new ArgumentException("Cannot emit a parameter list for an uncallable function since they lack ABI information.", nameof(declaration)); }

            bool first = true;

            bool writeImplicitParameters = mode is EmitParameterListMode.DllImportParameters or EmitParameterListMode.TrampolineArguments or EmitParameterListMode.VTableFunctionPointerParameters;
            bool writeTypes = mode is EmitParameterListMode.DllImportParameters or EmitParameterListMode.TrampolineParameters or EmitParameterListMode.VTableFunctionPointerParameters;
            bool writeNames = mode is EmitParameterListMode.DllImportParameters or EmitParameterListMode.TrampolineArguments or EmitParameterListMode.TrampolineParameters;
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
                void WriteOutReturnBuffer(EmitFunctionContext emitContext)
                {
                    if (!first)
                    { Writer.Write(", "); }

                    if (mode == EmitParameterListMode.TrampolineArguments)
                    { Writer.Write("&"); }

                    if (writeTypes)
                    {
                        WriteTypeAsReference(context, declaration, declaration.ReturnType);

                        if (writeNames)
                        { Writer.Write(' '); }
                    }

                    if (writeNames)
                    { Writer.WriteIdentifier(emitContext.ReturnBufferParameterName); }

                    first = false;
                }

                // Write out before-this return buffer
                if (declaration.ReturnByReference && !declaration.FunctionAbi.ReturnInfo.Flags.HasFlag(PathogenArgumentFlags.IsSRetAfterThis))
                { WriteOutReturnBuffer(emitContext); }

                // Write out the this pointer
                if (emitContext.ThisType is not null)
                {
                    if (!first)
                    { Writer.Write(", "); }

                    if (writeTypes)
                    {
                        WriteType(context, declaration, emitContext.ThisType);

                        if (writeNames)
                        { Writer.Write(' '); }
                    }

                    if (writeNames)
                    { Writer.WriteIdentifier(emitContext.ThisParameterName); }

                    first = false;
                }

                // Write out after-this return buffer
                if (declaration.ReturnByReference && declaration.FunctionAbi.ReturnInfo.Flags.HasFlag(PathogenArgumentFlags.IsSRetAfterThis))
                { WriteOutReturnBuffer(emitContext); }
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
                        if (mode == EmitParameterListMode.DllImportParameters && parameter.Type.IsCSharpType(context.Library, CSharpBuiltinType.Bool))
                        { Writer.Write("[MarshalAs(UnmanagedType.I1)] "); }

                        if (mode == EmitParameterListMode.TrampolineParameters)
                        { WriteTypeForTrampoline(parameterContext, parameter, parameter.Type); }
                        else
                        { WriteType(parameterContext, parameter, parameter.Type); }
                    }

                    if (writeNames)
                    { Writer.Write(' '); }
                }

                if (writeNames)
                { Writer.WriteIdentifier(parameter.Name); }

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

        private void WriteTypeForTrampoline(VisitorContext context, TranslatedDeclaration declaration, TypeReference type)
        {
            // For trampolines we want to hide the semantics of NativeBoolean/NativeChar so we silently replace them with their actual C# type
            // See https://github.com/InfectedLibraries/Biohazrd/issues/200 for details.
            if (type is TranslatedTypeReference typeReference)
            {
                switch (typeReference.TryResolve(context.Library))
                {
                    case NativeBooleanDeclaration:
                        WriteType(context, declaration, CSharpBuiltinType.Bool);
                        return;
                    case NativeCharDeclaration:
                        WriteType(context, declaration, CSharpBuiltinType.Char);
                        return;
                }
            }

            WriteType(context, declaration, type);
        }

        protected override void VisitParameter(VisitorContext context, TranslatedParameter declaration)
            => FatalContext(context, declaration);
    }
}
