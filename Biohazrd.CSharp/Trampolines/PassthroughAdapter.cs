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
        => writer.WriteIdentifier(ParameterName);
}
