using Biohazrd.CSharp.Infrastructure;
using Biohazrd.CSharp.Metadata;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Biohazrd.CSharp.Trampolines;

public sealed record Trampoline
{
    private Trampoline? Target { get; }
    internal DeclarationId TargetFunctionId { get; }
    public AccessModifier Accessibility { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }
    public IReturnAdapter ReturnAdapter { get; init; }
    public ImmutableArray<Adapter> Adapters { get; init; }

    [MemberNotNullWhen(false, nameof(Target))]
    public bool IsNativeFunction => Target is null;

    internal Trampoline(TranslatedFunction function, IReturnAdapter nativeReturnAdapter, ImmutableArray<Adapter> nativeAdapters)
    {
        Target = null;
        TargetFunctionId = function.Id;
        Accessibility = function.Accessibility;
        Name = function.Name;
        Description = function.IsVirtual ? "Native Virtual Method Pointer" : "Native P/Invoke";
        ReturnAdapter = nativeReturnAdapter;
        Adapters = nativeAdapters;
        Debug.Assert(IsNativeFunction);
    }

    internal Trampoline(TrampolineBuilder builder)
    {
        Trampoline? template = builder.TargetIsTemplate ? builder.Target : null;
        Target = template?.Target ?? builder.Target;
        Debug.Assert(Target is not null);
        TargetFunctionId = builder.Target.TargetFunctionId;
        Accessibility = builder.Accessibility;
        Name = builder.Name;
        Description = builder.Description;

        // Add return adapter
        if (builder.ReturnAdapter is not null)
        { ReturnAdapter = builder.ReturnAdapter; }
        else if (template?.ReturnAdapter is not null)
        { ReturnAdapter = template.ReturnAdapter; }
        else if (Target.ReturnAdapter.OutputType is VoidTypeReference)
        { ReturnAdapter = VoidReturnAdapter.Instance; }
        else
        { ReturnAdapter = new PassthroughReturnAdapter(Target.ReturnAdapter); }

        // Add parameter adapters
        int expectedLength;
        if (template is not null)
        { expectedLength = template.Adapters.Length; }
        else
        {
            expectedLength = 0;
            foreach (Adapter adapter in Target.Adapters)
            {
                if (adapter.AcceptsInput)
                { expectedLength++; }
            }
        }
        ImmutableArray<Adapter>.Builder adapters = ImmutableArray.CreateBuilder<Adapter>(expectedLength);

        foreach (Adapter targetAdapter in template?.Adapters ?? Target.Adapters)
        {
            // If the builder adapted this adapter, insert its adapter
            if (builder.Adapters?.TryGetValue(targetAdapter, out Adapter? adapter) ?? false)
            { adapters.Add(adapter); } //TODO: Debug.Assert(template is not null || targetAdapter.AcceptsInput); ? Is it enough this is done in Adapter's constructor? (Probably.)
            else if (builder.TargetIsTemplate)
            { adapters.Add(targetAdapter); }
            else if (targetAdapter.AcceptsInput)
            { adapters.Add(new PassthroughAdapter(targetAdapter)); }
        }

        Adapters = adapters.MoveToImmutable();
    }

    public Adapter? TryGetAdapterFor(SpecialAdapterKind specialKind)
    {
        foreach (Adapter adapter in Adapters)
        {
            if (adapter.SpecialKind == specialKind)
            { return adapter; }
        }

        return null;
    }

    //TODO: This is somewhat odd when we're already iterating through the parameters. Maybe we should provide a special enumerator?
    public Adapter? TryGetAdapterFor(TranslatedParameter parameter)
    {
        foreach (Adapter adapter in Adapters)
        {
            if (adapter.CorrespondsTo(parameter))
            { return adapter; }
        }

        return null;
    }

    internal void Write(ICSharpOutputGenerator outputGenerator, VisitorContext context, TranslatedFunction declaration, CSharpCodeWriter writer)
    {
        if (declaration.Id != TargetFunctionId && !declaration.ReplacedIds.Contains(TargetFunctionId))
        { throw new ArgumentException("The specified function is not related to the target of this trampoline.", nameof(declaration)); }

        // Don't emit the native function for virtual methods
        //TODO: Maybe we should use this to emit the function pointer?
        if (declaration.IsVirtual && IsNativeFunction)
        { return; }

        // If the function is virtual determine how to access it
        string? virtualMethodAccess = null;
        string? virtualMethodAccessFailure = null;

        if (declaration.IsVirtual && Target is not null && Target.IsNativeFunction)
        {
            // Figure out how to access the VTable entry
            if (context.ParentDeclaration is not TranslatedRecord record)
            { virtualMethodAccessFailure = $"Virtual method has no associated class."; }
            else if (record.VTableField is null)
            { virtualMethodAccessFailure = "Class has no vTable pointer."; }
            else if (record.VTable is null)
            { virtualMethodAccessFailure = "Class has no virtual method table."; }
            else if (declaration.Declaration is null)
            { virtualMethodAccessFailure = "Virtual method has no associated Clang declaration."; }
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
                { virtualMethodAccessFailure = "Could not find entry in virtual method table."; }
                else
                { virtualMethodAccess = $"{SanitizeIdentifier(record.VTableField.Name)}->{SanitizeIdentifier(vTableEntry.Name)}"; }
            }

            if (virtualMethodAccessFailure is not null)
            { outputGenerator.AddDiagnostic(Severity.Error, $"Method trampoline cannot be emitted: {virtualMethodAccessFailure}"); }

            Debug.Assert(virtualMethodAccess is not null || virtualMethodAccessFailure is not null, "We need either a virtual method access or a failure message.");
        }

        // Create base contexts
        TrampolineContext functionContext = new(outputGenerator, this, writer, context, declaration);
        TrampolineContext parameterContext = functionContext with { Context = functionContext.Context.Add(declaration) };

        // Scan adapters to figure out how this function will be emitted
        bool hasExplicitThis = false;
        bool hasAnyEpilogue = false;
        int firstDefaultableInput = int.MaxValue;
        {
            int adapterIndex = -1;
            foreach (Adapter adapter in Adapters)
            {
                adapterIndex++;

                if (adapter.AcceptsInput)
                {
                    if (adapter.SpecialKind == SpecialAdapterKind.ThisPointer)
                    { hasExplicitThis = true; }

                    //TODO: I think it'd be better for CanEmitDefaultValue to imply that a default value will be emitted if possible.
                    // This allows special adapters which have a null DefaultValue but can write their own anyway
                    // It also means we can make it more opt-in so adapters have to handle UnsupportedConstantValue, etc.
                    if (adapter.CanEmitDefaultValue && adapter.DefaultValue is not null)
                    {
                        if (firstDefaultableInput == int.MaxValue)
                        { firstDefaultableInput = adapterIndex; }
                    }
                    else
                    { firstDefaultableInput = int.MaxValue; }
                }

                if (adapter is IAdapterWithEpilogue)
                { hasAnyEpilogue = true; }
            }
        }

        //===========================================================================================================================================
        // Emit attributes
        //===========================================================================================================================================
        // Hide from Intellisense if applicable
        // (Only bother doing this if the method is externally visible
        if (Accessibility > AccessModifier.Private)
        {
            if (declaration.Metadata.Has<HideDeclarationFromIntellisense>())
            {
                writer.Using("System.ComponentModel");
                writer.WriteLine("[EditorBrowsable(EditorBrowsableState.Never)]");
            }
        }

        // Emit DllImport attribute
        if (IsNativeFunction)
        {
            writer.Using("System.Runtime.InteropServices");
            writer.Write($"[DllImport(\"{SanitizeStringLiteral(declaration.DllFileName)}\", CallingConvention = CallingConvention.{declaration.CallingConvention}");

            if (declaration.MangledName != Name)
            { writer.Write($", EntryPoint = \"{SanitizeStringLiteral(declaration.MangledName)}\""); }

            //TODO: Allow SetLastError on .NET 5?

            writer.WriteLine(", ExactSpelling = true)]");
        }
        else
        {
            //TODO: This seems like it should be more extensible
            // Hide from the debugger if applicable
            if (outputGenerator.Options.HideTrampolinesFromDebugger)
            {
                writer.Using("System.Diagnostics");
                writer.WriteLine("[DebuggerStepThrough, DebuggerHidden]");
            }

            EmitMethodImplAttribute(declaration, writer);

            // Obsolete virtual method if it's broken
            if (virtualMethodAccessFailure is not null)
            {
                writer.Using("System"); // ObsoleteAttribute
                writer.WriteLine($"[Obsolete(\"Method is broken: {SanitizeStringLiteral(virtualMethodAccessFailure)}\", error: true)]");
            }
        }

        //===========================================================================================================================================
        // Emit function signature
        //===========================================================================================================================================
        {
            if (GetConstructorName(outputGenerator, context, declaration) is string constructorName)
            {
                writer.Write(Accessibility.ToCSharpKeyword());
                writer.Write(' ');
                writer.WriteIdentifier(constructorName);
            }
            else
            {
                writer.Write(Accessibility.ToCSharpKeyword());

                // Write out the static keyword if this is a static method or this is an instance method that still has an explicit this pointer
                if (!declaration.IsInstanceMethod || hasExplicitThis)
                { writer.Write(" static"); }

                // Write out extern if this is the P/Invoke
                if (IsNativeFunction)
                { writer.Write(" extern"); }

                // Write out the return type
                writer.Write(' ');
                ReturnAdapter.WriteReturnType(functionContext, writer);
                writer.Write(' ');

                // Write out the function name
                writer.WriteIdentifier(Name);
            }

            // Write out the parameter list
            writer.Write('(');

            bool first = true;
            int adapterIndex = -1;
            bool skipDefaultValues = outputGenerator.Options.SuppressDefaultParameterValuesOnNonPublicMethods && Accessibility != AccessModifier.Public;
            foreach (Adapter adapter in Adapters)
            {
                adapterIndex++;

                // Nothing to do if the adapter doesn't accept input
                if (!adapter.AcceptsInput)
                { continue; }

                // Write out the adapter's parameter
                if (first)
                { first = false; }
                else
                { writer.Write(", "); }

                bool emitDefaultValue = !skipDefaultValues && adapterIndex >= firstDefaultableInput;
                if (emitDefaultValue)
                { Debug.Assert(adapter.CanEmitDefaultValue && adapter.DefaultValue is not null, "Tried to emit a default value when a parameter can't emit a default value or doesn't have one."); }
                adapter.WriteInputParameter(parameterContext, writer, emitDefaultValue);
            }

            if (IsNativeFunction)
            {
                writer.WriteLine(");");
                return;
            }
            else
            { writer.WriteLine(')'); }
        }

        //===========================================================================================================================================
        // Emit the function body
        //===========================================================================================================================================

        // If this is a broken virtual method trampoline the body should just throw
        if (virtualMethodAccessFailure is not null)
        {
            Debug.Assert(declaration.IsVirtual);
            writer.Using("System"); // PlatformNotSupportedException
            writer.WriteLineIndented($"=> throw new PlatformNotSupportedException(\"Method is broken: {SanitizeStringLiteral(virtualMethodAccessFailure)}\");");
            return;
        }

        using (writer.Block())
        {
            IShortReturnAdapter? shortReturn = hasAnyEpilogue ? null : ReturnAdapter as IShortReturnAdapter;

            // Emit out prologues
            if (shortReturn is not null)
            { shortReturn.WriteShortPrologue(functionContext, writer); }
            else
            { ReturnAdapter.WritePrologue(functionContext, writer); }

            foreach (Adapter adapter in Adapters)
            { adapter.WritePrologue(parameterContext, writer); }

            // Emit blocks (IE: `fixed` statements)
            writer.EnsureSeparation();
            bool hasBlocks = false;
            foreach (Adapter adapter in Adapters)
            {
                if (adapter.WriteBlockBeforeCall(parameterContext, writer))
                { hasBlocks = true; }
            }

            // Emit function dispatch
            if (hasBlocks)
            { writer.Write("{ "); }

            if (shortReturn is not null)
            { shortReturn.WriteShortReturn(functionContext, writer); }
            else
            { ReturnAdapter.WriteResultCapture(functionContext, writer); }

            if (virtualMethodAccess is not null)
            {
                Debug.Assert(declaration.IsVirtual);
                writer.Write(virtualMethodAccess);
            }
            else if (Target.GetConstructorName(outputGenerator, context, declaration) is string constructorName)
            {
                writer.Write("this = new ");
                writer.WriteIdentifier(constructorName);
            }
            else
            //TODO: Need to handle constructor dispatch
            { writer.WriteIdentifier(Target.Name); }

            // Emit function arguments
            writer.Write('(');

            bool first = true;
            ImmutableArray<TranslatedParameter> parameters = declaration.Parameters;
            int adapterIndex = -1;

            foreach (Adapter adapter in Adapters)
            {
                adapterIndex++;

                if (!adapter.ProvidesOutput)
                { continue; }

                if (first)
                { first = false; }
                else
                { writer.Write(", "); }

                adapter.WriteOutputArgument(parameterContext, writer);
            }

            writer.Write(");");

            // Finish function dispatch
            if (hasBlocks)
            { writer.WriteLine(" }"); }
            else
            { writer.WriteLine(); }

            // Emit epilogues
            if (hasAnyEpilogue || shortReturn is null)
            {
                writer.EnsureSeparation();

                if (hasAnyEpilogue)
                {
                    foreach (Adapter adapter in Adapters)
                    {
                        if (adapter is IAdapterWithEpilogue epilogueAdapter)
                        { epilogueAdapter.WriteEpilogue(parameterContext, writer); }
                    }
                }

                ReturnAdapter.WriteEpilogue(parameterContext, writer);
            }
        }
    }

    private void EmitMethodImplAttribute(TranslatedFunction declaration, CSharpCodeWriter writer)
    {
        if (!declaration.Metadata.TryGet<TrampolineMethodImplOptions>(out TrampolineMethodImplOptions optionsMetadata))
        { return; }

        MethodImplOptions options = optionsMetadata.Options;

        if (options == 0)
        { return; }

        writer.Using("System.Runtime.CompilerServices");
        writer.Write("[MethodImpl(");

        bool first = true;
        foreach (MethodImplOptions option in Enum.GetValues<MethodImplOptions>())
        {
            if ((options & option) == option)
            {
                if (first)
                { first = false; }
                else
                { writer.Write(" | "); }

                writer.Write($"{nameof(MethodImplOptions)}.{option}");
                options &= ~option;
            }
        }

        if (first || options != 0)
        {
            if (!first)
            { writer.Write(" | "); }

            writer.Write($"({nameof(MethodImplOptions)}){(int)options}");
        }

        writer.WriteLine(")]");
    }

    private string? GetConstructorName(ICSharpOutputGenerator outputGenerator, VisitorContext context, TranslatedFunction declaration)
    {
        // Native functions are never emitted as constructors
        if (IsNativeFunction)
        { return null; }

        // Non-constructors are never emitted as constructors
        if (declaration.SpecialFunctionKind != SpecialFunctionKind.Constructor)
        { return null; }

        // If we don't have a parent record we don't know what type we're constructing
        if (context.ParentDeclaration is not TranslatedRecord constructorType)
        { return null; }

        // If none of our adaptors accept inputs, we won't emit any parameters. Parameterless constructors require C# 10 or newer
        if (outputGenerator.Options.TargetLanguageVersion < TargetLanguageVersion.CSharp10)
        {
            bool haveInputs = false;
            foreach (Adapter adapter in Adapters)
            {
                if (adapter.AcceptsInput)
                {
                    haveInputs = true;
                    break;
                }
            }

            if (!haveInputs)
            { return null; }
        }

        // If we got this far we will emit as a constructor
        return constructorType.Name;
    }
}
