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

    internal DeclarationId TargetDeclaration { get; }
    public ConstantValue? DefaultValue { get; protected init; }
    public virtual bool CanEmitDefaultValue
        //TODO: Ideally this should check InputType to see if the default value is actually compatible (and allow string constants if the input type is C#'s string type.)
        // There partially-finished logic for this in CSharpTranslationVerifier.TransformParameter which should be moved here, but it requires access to the TranslatedLibrary which we don't have here.
        => DefaultValue switch
        {
            StringConstant => false,
            UnsupportedConstantExpression => false,
            null => false,
            _ => true
        };

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

    private protected Adapter(TypeReference inputType, string parameterName)
    {
        InputType = inputType;
        ParameterName = parameterName;
        AcceptsInput = true;

        TargetDeclaration = DeclarationId.Null;
        DefaultValue = null;

        SpecialKind = SpecialAdapterKind.None;
    }

    public bool CorrespondsTo(TranslatedParameter parameter)
    {
        // Early out: Special adapters never correspond to parameters
        if (SpecialKind != SpecialAdapterKind.None)
        { return false; }

        // If we don't have a target (IE: This is an input-only adapter) we can't correspond to a parameter
        if (TargetDeclaration == DeclarationId.Null)
        { return false; }

        return parameter.MatchesId(TargetDeclaration);
    }

    public virtual void WriteInputType(TrampolineContext context, CSharpCodeWriter writer)
    {
        if (!AcceptsInput)
        { throw new InvalidOperationException("This adapter does not accept input."); }

        context.WriteType(InputType);
    }

    public virtual void WriteInputParameter(TrampolineContext context, CSharpCodeWriter writer, bool emitDefaultValue)
    {
        WriteInputType(context, writer);
        writer.Write(' ');
        writer.WriteIdentifier(ParameterName);

        if (emitDefaultValue)
        {
            if (!CanEmitDefaultValue)
            { throw new InvalidOperationException("Caller requested we emit the default value but it's not supported by this adapter."); }

            // It is possible for implementations to override `CanEmitDefaultValue` to return `true` even when `DefaultValue`.
            // This is OK (it's assumed the implementation has special default value logic) but it's not OK for it to not override us to do the emit.
            if (DefaultValue is null)
            { throw new NotSupportedException($"Default implementation of {nameof(WriteInputParameter)} cannot emit a default value without {nameof(DefaultValue)} being set."); }

            writer.Write(" = ");
            context.WriteConstant(DefaultValue, InputType);
        }
    }

    public abstract void WritePrologue(TrampolineContext context, CSharpCodeWriter writer);
    public abstract bool WriteBlockBeforeCall(TrampolineContext context, CSharpCodeWriter writer);
    public abstract void WriteOutputArgument(TrampolineContext context, CSharpCodeWriter writer);
}
