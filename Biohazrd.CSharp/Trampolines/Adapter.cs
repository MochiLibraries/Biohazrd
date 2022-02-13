using Biohazrd.CSharp.Infrastructure;
using Biohazrd.Expressions;
using System;

namespace Biohazrd.CSharp.Trampolines;

public abstract class Adapter
{
    public TypeReference InputType { get; protected init; }
    public string Name { get; protected init; }
    public bool AcceptsInput { get; protected init; }
    public bool ProvidesOutput => TargetDeclaration != DeclarationId.Null;

    internal DeclarationId TargetDeclaration { get; }
    public ConstantValue? DefaultValue { get; protected init; }

    public SpecialAdapterKind SpecialKind { get; internal init; }

    private protected Adapter(TranslatedParameter target)
    {
        InputType = target.Type;
        Name = target.Name;
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
            Name = "this";
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
            Name = "__returnBuffer";
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
        Name = target.Name;
        AcceptsInput = true;

        TargetDeclaration = target.TargetDeclaration;
        DefaultValue = target.DefaultValue;

        SpecialKind = target.SpecialKind;
    }

    private protected Adapter(TypeReference inputType, string parameterName)
    {
        InputType = inputType;
        Name = parameterName;
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

    //TODO: Use an analyzer to enforce that this is overriden if WriteInputParameter is overridden
    public virtual bool CanEmitDefaultValue(TranslatedLibrary library)
    {
        // Can't emit a default value if we don't accept input
        if (!AcceptsInput)
        { throw new InvalidOperationException("This adapter does not accept input."); }

        // Can't emit a default value when there isn't one
        if (DefaultValue is null)
        { return false; }

        // Can't emit a default value when the constant isn't supported by Biohazrd
        if (DefaultValue is UnsupportedConstantExpression)
        { return false; }

        // Figure out the effective input type
        TypeReference inputType = InputType;
        {
            // If the input type is a byref we only support default values if its in-byref
            if (inputType is ByRefTypeReference byRefInputType)
            {
                if (byRefInputType.Kind != ByRefKind.In)
                { return false; }

                inputType = byRefInputType.Inner;
            }

            // If the input type is a typedef we redsolve it
            while (inputType is TranslatedTypeReference translatedType)
            {
                if (translatedType.TryResolve(library) is TranslatedTypedef typedef)
                { inputType = typedef.UnderlyingType; }
                else
                { break; }
            }
        }

        // Check if the constant is compatible with the type
        switch (DefaultValue)
        {
            // Assume custom types are compatible
            //TODO: Maybe ICustomCSharpConstantValue should have to implement a method for checking?
            case ICustomCSharpConstantValue:
                return true;
            // Double and float constants are supported with their corresponding C# built-in types
            case DoubleConstant:
                return inputType == CSharpBuiltinType.Double;
            case FloatConstant:
                return inputType == CSharpBuiltinType.Float;
            // Integer constants are supported for all C# built-ins as well as enums
            //TODO: How should we handle overflow/underflow here? Right now it's not handled at all and will result in invalid codegen.
            // We could reject it here, but I think a better solution would be a verification warning and an unchecked cast on emit.
            // (This should never happen unless a generator author forces it to happen with a transformation, so let's wait and see what the real-world scenario is.)
            case IntegerConstant:
            {
                if (inputType is CSharpBuiltinTypeReference)
                { return true; }
                else if (inputType == CSharpBuiltinType.NativeInt || inputType == CSharpBuiltinType.NativeUnsignedInt)
                { return true; }
                else if (inputType is TranslatedTypeReference translatedType && translatedType.TryResolve(library) is TranslatedEnum)
                { return true; }
                else
                { return false; }
            }
            // Nulls can be emitted for pointer types
            case NullPointerConstant:
                return inputType is PointerTypeReference or FunctionPointerTypeReference;
            // Strings are not supported by default
            case StringConstant:
                return inputType == CSharpBuiltinType.String;
            default:
                return false;
        }
    }

    public virtual void WriteInputParameter(TrampolineContext context, CSharpCodeWriter writer, bool emitDefaultValue)
    {
        WriteInputType(context, writer);
        writer.Write(' ');
        writer.WriteIdentifier(Name);

        if (emitDefaultValue)
        {
            //TODO: Should this be an assert? The default implementation of CanEmitDefaultValue is a bit heavy, doesn't cache, and a well-formed application
            // will not call us with emitDefaultValue = true when it returns false. Alternatively we could just add caching to CanEmitDefaultValue.
            if (!CanEmitDefaultValue(context.Context.Library))
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
