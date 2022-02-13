using Biohazrd.Expressions;
using System;
using System.Diagnostics;

namespace Biohazrd.CSharp.Trampolines;

public abstract class Adapter
{
    public TypeReference InputType { get; protected init; }
    public string ParameterName { get; protected init; } //TODO: Should this just be `Name`?
    public bool AcceptsInput { get; protected init; }
    public bool ProvidesOutput => TargetDeclaration != DeclarationId.Null;

    //TODO: We should provide a constructor that allows synthesizing parameters which won't be passed to the target function
    //TODO: Is it OK to use a raw declaration id here instead of a DeclarationReference? It's more efficient and we can check if we match a parameter without doing full resolution
    internal DeclarationId TargetDeclaration { get; }
    //TODO: How will validation remove default values which can't be emitted in C#? How does it even determine if the default value can or can't be handled by this adapter?
    public ConstantValue? DefaultValue { get; protected init; }
    public abstract bool CanEmitDefaultValue { get; }

    public SpecialAdapterKind SpecialKind { get; internal init; }

    private protected Adapter(TranslatedParameter target)
    {
        InputType = target.Type;
        ParameterName = target.Name;
        AcceptsInput = true;

        TargetDeclaration = target.Id;
        DefaultValue = target.DefaultValue;

        SpecialKind = SpecialAdapterKind.None;
    }

    private protected Adapter(TranslatedFunction target, SpecialAdapterKind specialKind, TypeReference inputType)
    {
        AcceptsInput = true;
        TargetDeclaration = target.Id;
        DefaultValue = null;
        SpecialKind = specialKind;

        if (specialKind == SpecialAdapterKind.ThisPointer)
        {
            if (!target.IsInstanceMethod)
            { throw new ArgumentException("The specified function is not an instance method and as such cannot have a this pointer parameter.", nameof(target)); }

            if (inputType is not PointerTypeReference)
            { throw new ArgumentException($"The this pointer parameter '{inputType}' is not a pointer type.", nameof(inputType)); }
            
            InputType = inputType;
            ParameterName = "this";
        }
        else if (specialKind == SpecialAdapterKind.ReturnBuffer)
        {
            if (!target.ReturnByReference)
            { throw new ArgumentException("The specified function does not implicitly return by reference and as such cannot have a return buffer parameter.", nameof(target)); }

            if (inputType is not PointerTypeReference pointerType)
            { throw new ArgumentException($"The return buffer parameter '{inputType}' is not a pointer type.", nameof(inputType)); }
            else if (pointerType.Inner != target.ReturnType)
            { throw new ArgumentException($"The return buffer parameter '{inputType}' is not a pointer to the function's return type '{target.ReturnType}'.", nameof(inputType)); }

            InputType = inputType;
            ParameterName = "__returnBuffer";
        }
        else if (specialKind == SpecialAdapterKind.None)
        { throw new ArgumentException("Adapters targeting functions rather than their parameters must have a special kind.", nameof(specialKind)); }
        else
        { throw new ArgumentException($"The specified special kind '{specialKind}' is invalid or unsupported.", nameof(specialKind)); }
    }

    protected Adapter(Adapter target)
    {
        if (!target.AcceptsInput)
        { throw new ArgumentException("The target adapter does not accept an input!", nameof(target)); }

        InputType = target.InputType;
        ParameterName = target.ParameterName;
        AcceptsInput = true;

        TargetDeclaration = target.TargetDeclaration;
        DefaultValue = target.DefaultValue;

        SpecialKind = target.SpecialKind;
    }

    public bool CorrespondsTo(TranslatedParameter parameter)
    {
        // Early out: Special adapters never correspond to parameters
        if (SpecialKind != SpecialAdapterKind.None)
        { return false; }

        // If we don't have a target (IE: This is an input-only adapter) we can't correspond to a parameter
        if (TargetDeclaration == DeclarationId.Null)
        { return false; }

        return parameter.Id == TargetDeclaration || parameter.ReplacedIds.Contains(TargetDeclaration);
    }

    public abstract void WriteInputParameter(TrampolineContext context, CSharpCodeWriter writer, bool emitDefaultValue);
    public abstract void WritePrologue(TrampolineContext context, CSharpCodeWriter writer);
    public abstract bool WriteBlockBeforeCall(TrampolineContext context, CSharpCodeWriter writer);
    public abstract void WriteOutputArgument(TrampolineContext context, CSharpCodeWriter writer);
}
