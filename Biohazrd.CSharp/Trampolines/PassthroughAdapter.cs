namespace Biohazrd.CSharp.Trampolines;

public sealed class PassthroughAdapter : Adapter
{
    public override bool CanEmitDefaultValue => true;

    internal PassthroughAdapter(TranslatedParameter target)
        : base(target)
    { }

    internal PassthroughAdapter(TranslatedParameter target, TypeReference forcedInputType)
        : base(target)
        => InputType = forcedInputType;

    internal PassthroughAdapter(TranslatedFunction target, SpecialAdapterKind specialKind, TypeReference inputType)
        : base(target, specialKind, inputType)
    { }

    public PassthroughAdapter(Adapter target)
        : base(target)
    { }

    public override void WriteInputParameter(TrampolineContext context, CSharpCodeWriter writer, bool emitDefaultValue)
    {
        context.WriteType(InputType);
        writer.Write(' ');
        writer.WriteIdentifier(ParameterName);

        if (emitDefaultValue && DefaultValue is not null)
        {
            writer.Write(" = ");
            context.WriteConstant(DefaultValue, InputType);
        }
    }

    public override void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    public override bool WriteBlockBeforeCall(TrampolineContext context, CSharpCodeWriter writer)
        => false;

    public override void WriteOutputArgument(TrampolineContext context, CSharpCodeWriter writer)
    {
        if (InputType is ByRefTypeReference byRefType)
        {
            // In theory we don't have to do this for `in` parameters, but C# allows you to overload between byref in and by value.
            // Detecting if this was done is more effort than it's wroth to save 3 characters, so we write out an explicit `in` keyword to ensure the correct overload is chosen.
            writer.Write(byRefType.Kind.GetKeyword());
            writer.Write(' ');
        }

        writer.WriteIdentifier(ParameterName);
    }
}
