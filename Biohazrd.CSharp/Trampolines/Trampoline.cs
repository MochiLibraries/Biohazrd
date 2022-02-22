using Biohazrd.CSharp.Infrastructure;
using Biohazrd.CSharp.Metadata;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Biohazrd.CSharp.Trampolines;

public sealed record Trampoline
{
    internal Trampoline? Target { get; }
    internal DeclarationId TargetFunctionId { get; }
    public AccessModifier Accessibility { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }
    public IReturnAdapter ReturnAdapter { get; init; }
    public ImmutableArray<Adapter> Adapters { get; init; }

    [MemberNotNullWhen(false, nameof(Target))]
    public bool IsNativeFunction => Target is null;

    /// <summary>Enables <see cref="DllImportAttribute.SetLastError"/> on the emitted P/Invoke.</summary>
    /// <remarks>
    /// This is a property of the trampoline in order to ensure consistent emit between .NET 5 and more modern runtimes.
    /// It should not be used on .NET 6 or newer or exposed publicly.
    /// <c>SetLastError</c> behavior should be controlled by applying <see cref="SetLastErrorFunction"/> prior to <see cref="CreateTrampolinesTransformation"/>.
    /// </remarks>
    internal bool UseLegacySetLastError { get; init; }

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
        Debug.Assert(ReturnAdapter is not IAdapterWithGenericParameter && !Adapters.OfType<IAdapterWithGenericParameter>().Any(), "Native functions cannot have generic parameters.");
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

        if (builder.SyntheticAdapters is not null)
        { expectedLength += builder.SyntheticAdapters.Count; }

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

        if (builder.SyntheticAdapters is not null)
        { adapters.AddRange(builder.SyntheticAdapters); }

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

    internal void Emit(ICSharpOutputGenerator outputGenerator, VisitorContext context, TranslatedFunction declaration, CSharpCodeWriter writer)
    {
        if (!declaration.MatchesId(TargetFunctionId))
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

        // Create base function context
        TrampolineContext functionContext = new(outputGenerator, this, writer, context, declaration);

        // Scan adapters to figure out how this function will be emitted and to build adapter contexts
        bool hasExplicitThis = false;
        bool hasAnyEpilogue = false;
        bool hasInnerWrapper = false;
        bool hasGenericParameters = ReturnAdapter is IAdapterWithGenericParameter;
        bool hasDoubleDutyReturnAdapter = false;
        int firstDefaultableInput = int.MaxValue;
        TrampolineContext[] adapterContexts = new TrampolineContext[Adapters.Length];
        {
            int adapterIndex = -1;
            VisitorContext parameterVisitorContext = functionContext.Context.Add(declaration);
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

                if (adapter is IAdapterWithInnerWrapper)
                { hasInnerWrapper = hasAnyEpilogue = true; }

                if (adapter is IAdapterWithGenericParameter)
                { hasGenericParameters = true; }

                if (ReferenceEquals(adapter, ReturnAdapter))
                { hasDoubleDutyReturnAdapter = true; }

                // Determine the context for this adapter
                adapterContexts[adapterIndex] = DetermineAdapterContext(declaration, parameterVisitorContext, adapter, functionContext);
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

            if (UseLegacySetLastError)
            {
                //Debug.Assert(outputGenerator.Options.TargetRuntime < TargetRuntime.Net6);
                writer.Write(", SetLastError = true");
            }

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

            // Write out generic parameters
            if (hasGenericParameters)
            {
                Debug.Assert(!IsNativeFunction);
                writer.Write('<');
                bool first = true;

                if (!hasDoubleDutyReturnAdapter && ReturnAdapter is IAdapterWithGenericParameter genericReturnAdapter)
                {
                    genericReturnAdapter.WriteGenericParameter(functionContext, writer);
                    first = false;
                }

                int adapterIndex = -1;
                foreach (Adapter adapter in Adapters)
                {
                    adapterIndex++;

                    if (adapter is not IAdapterWithGenericParameter genericAdapter)
                    { continue; }

                    if (first)
                    { first = false; }
                    else
                    { writer.Write(", "); }

                    genericAdapter.WriteGenericParameter(adapterContexts[adapterIndex], writer);
                }
                writer.Write('>');
            }

            // Write out the parameter list
            {
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
                    adapter.WriteInputParameter(adapterContexts[adapterIndex], writer, emitDefaultValue);
                }

                if (IsNativeFunction)
                {
                    writer.WriteLine(");");
                    return;
                }
                else
                { writer.WriteLine(')'); }
            }

            // Write out generic constraints
            if (hasGenericParameters)
            {
                using (writer.Indent())
                {
                    if (!hasDoubleDutyReturnAdapter && ReturnAdapter is IAdapterWithGenericParameter genericReturnAdapter)
                    { genericReturnAdapter.WriteGenericConstraint(functionContext, writer); }

                    int adapterIndex = -1;
                    foreach (Adapter adapter in Adapters)
                    {
                        adapterIndex++;

                        if (adapter is not IAdapterWithGenericParameter genericAdapter)
                        { continue; }

                        genericAdapter.WriteGenericConstraint(adapterContexts[adapterIndex], writer);
                    }
                }
            }
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

            if (shortReturn is not null && !shortReturn.CanEmitShortReturn)
            { shortReturn = null; }

            // Emit out prologues
            {
                if (shortReturn is not null)
                { shortReturn.WriteShortPrologue(functionContext, writer); }
                else
                { ReturnAdapter.WritePrologue(functionContext, writer); }

                int adapterIndex = -1;
                foreach (Adapter adapter in Adapters)
                {
                    adapterIndex++;
                    adapter.WritePrologue(adapterContexts[adapterIndex], writer);
                }
            }

            // Emit blocks (IE: `fixed` statements)
            writer.EnsureSeparation();
            bool hasBlocks = false;
            {
                int adapterIndex = -1;
                foreach (Adapter adapter in Adapters)
                {
                    adapterIndex++;

                    if (adapter.WriteBlockBeforeCall(adapterContexts[adapterIndex], writer))
                    { hasBlocks = true; }
                }
            }

            // Emit function dispatch
            void EmitFunctionDispatch()
            {
                // Handle inner wrapper prologues
                if (hasInnerWrapper)
                {
                    int adapterIndex = -1;
                    foreach (Adapter adapter in Adapters)
                    {
                        adapterIndex++;
                        if (adapter is IAdapterWithInnerWrapper innerWrapper)
                        { innerWrapper.WriterInnerPrologue(adapterContexts[adapterIndex], writer); }
                    }
                }

                // Emit the actual function dispatch line
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
                {
                    writer.Write('(');

                    bool first = true;
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

                        adapter.WriteOutputArgument(adapterContexts[adapterIndex], writer);
                    }

                    writer.Write(");");
                }

                // Handle inner wrapper epilogues
                if (hasInnerWrapper)
                {
                    writer.WriteLine();
                    int adapterIndex = -1;
                    foreach (Adapter adapter in Adapters)
                    {
                        adapterIndex++;
                        if (adapter is IAdapterWithInnerWrapper innerWrapper)
                        { innerWrapper.WriteInnerEpilogue(adapterContexts[adapterIndex], writer); }
                    }
                }
            }

            if (hasInnerWrapper && hasBlocks)
            {
                using (writer.Block())
                { EmitFunctionDispatch(); }
            }
            else if (hasBlocks)
            {
                // If we need a block but there's no inner wrappers (IE: the dispatch will be on a single line) prefer to write it on a single line to keep things terse.
                writer.Write("{ ");
                EmitFunctionDispatch();
                writer.WriteLine(" }");
            }
            else
            {
                EmitFunctionDispatch();

                // If we didn't write inner wrappers, EmitFunctionDispatch will not finish the dispatch with a newline.
                if (!hasInnerWrapper)
                { writer.WriteLine(); }
            }

            // Emit epilogues
            if (hasAnyEpilogue || shortReturn is null)
            {
                writer.EnsureSeparation();

                if (hasAnyEpilogue)
                {
                    int adapterIndex = -1;
                    foreach (Adapter adapter in Adapters)
                    {
                        adapterIndex++;

                        if (adapter is IAdapterWithEpilogue epilogueAdapter)
                        { epilogueAdapter.WriteEpilogue(adapterContexts[adapterIndex], writer); }
                    }
                }

                ReturnAdapter.WriteEpilogue(functionContext, writer);
            }
        }
    }

    internal void EmitFunctionPointer(ICSharpOutputGeneratorInternal outputGenerator, VisitorContext context, TranslatedFunction declaration, CSharpCodeWriter writer)
    {
        if (IsNativeFunction)
        {
            string? callingConventionString = declaration.CallingConvention switch
            {
                CallingConvention.Cdecl => "Cdecl",
                CallingConvention.StdCall => "Stdcall",
                CallingConvention.ThisCall => "Thiscall",
                CallingConvention.FastCall => "Fastcall",
                _ => null
            };

            if (callingConventionString is null)
            {
                outputGenerator.Fatal(context, declaration, $"The {declaration.CallingConvention} convention is not supported.");
                writer.Write("void*");
                return;
            }

            writer.Write($"delegate* unmanaged[{callingConventionString}]<");
        }
        else
        { writer.Write($"delegate*<"); }

        // Create base contexts
        TrampolineContext functionContext = new(outputGenerator, this, writer, context, declaration);
        VisitorContext parameterVisitorContext = functionContext.Context.Add(declaration);

        foreach (Adapter adapter in Adapters)
        {
            if (!adapter.AcceptsInput)
            { continue; }

            TrampolineContext adapterContext = DetermineAdapterContext(declaration, parameterVisitorContext, adapter, functionContext);
            adapter.WriteInputType(adapterContext, writer);
            writer.Write(", ");
        }

        ReturnAdapter.WriteReturnType(functionContext, writer);
        writer.Write('>');
    }

    private TrampolineContext DetermineAdapterContext(TranslatedFunction declaration, in VisitorContext parameterVisitorContext, Adapter adapter, in TrampolineContext functionContext)
    {
        // This is a lot of extra mostly-unecessary work.
        // If this shows up on a profiler we should just remove it change the expectations of the context provided to GetTypeAsString and eliminate it.
        // https://github.com/MochiLibraries/Biohazrd/issues/238
        if (adapter.TargetDeclaration == DeclarationId.Null || declaration.MatchesId(adapter.TargetDeclaration))
        { return functionContext; }
        else
        {
            foreach (TranslatedParameter parameter in declaration.Parameters)
            {
                if (parameter.MatchesId(adapter.TargetDeclaration))
                {
                    return functionContext with
                    {
                        Context = parameterVisitorContext,
                        Declaration = parameter
                    };
                }
            }

            // This adapter does not match the function or any of its parameters, just use the function's context
            Debug.Fail("This should not happen.");
            return functionContext;
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

    public override string ToString()
        => $"{Name} ({Description})";
}
