using System;

namespace Biohazrd.CSharp.Trampolines;

public sealed class ByRefAdapter : Adapter
{
    public override bool CanEmitDefaultValue => base.CanEmitDefaultValue && Kind == ByRefKind.In;

    private TypeReference OutputType { get; }
    private string TemporaryName { get; }

    public ByRefKind Kind { get; }

    public ByRefAdapter(Adapter target, ByRefKind kind)
        : base(target)
    {
        if (target.InputType is not PointerTypeReference pointerType)
        { throw new ArgumentException("By ref adapters must target pointers!", nameof(target)); }

        if (!Enum.IsDefined(kind))
        { throw new ArgumentOutOfRangeException(nameof(kind)); }
        else if (kind == ByRefKind.RefReadOnly)
        { throw new ArgumentException("ref readonly is not valid in this context.", nameof(kind)); }

        OutputType = pointerType;
        InputType = new ByRefTypeReference(kind, pointerType.Inner);
        ParameterName = target.ParameterName;
        TemporaryName = $"__{ParameterName}P";
        Kind = kind;
    }

    public override void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    public override bool WriteBlockBeforeCall(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.Write("fixed (");
        context.WriteType(OutputType);
        writer.Write(' ');
        writer.WriteIdentifier(TemporaryName);
        writer.Write(" = &");
        writer.WriteIdentifier(ParameterName);
        writer.WriteLine(')');
        return true;
    }

    public override void WriteOutputArgument(TrampolineContext context, CSharpCodeWriter writer)
        => writer.WriteIdentifier(TemporaryName);
}
