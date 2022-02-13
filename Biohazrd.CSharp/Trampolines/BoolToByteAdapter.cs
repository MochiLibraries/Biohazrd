using System;

namespace Biohazrd.CSharp.Trampolines;

public sealed class BoolToByteAdapter : Adapter
{
    public override bool CanEmitDefaultValue => true;

    public BoolToByteAdapter(Adapter target)
        : base(target)
    {
        if (target.InputType != CSharpBuiltinType.Byte)
        { throw new ArgumentException("The target adapter does not take a byte!", nameof(target)); }

        InputType = CSharpBuiltinType.Bool;
        ParameterName = target.ParameterName;
    }

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
        writer.Using("System.Runtime.CompilerServices"); // Unsafe
        writer.Write("Unsafe.As<bool, byte>(ref ");
        writer.WriteIdentifier(ParameterName);
        writer.Write(')');
    }
}
