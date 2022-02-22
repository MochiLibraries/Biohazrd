using System;

namespace Biohazrd.CSharp.Trampolines;

public sealed class CastAdapter : Adapter
{
    public override bool CanEmitDefaultValue => true;
    public TypeReference TargetType { get; }
    public CastKind Kind { get; }

    public CastAdapter(Adapter target, TypeReference inputType, CastKind kind)
        : base(target)
    {
        if (!Enum.IsDefined(kind))
        { throw new ArgumentOutOfRangeException(nameof(kind)); }

        TargetType = target.InputType;
        InputType = inputType;
        Kind = kind;
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
        switch (Kind)
        {
            case CastKind.Explicit:
                writer.Write('(');
                context.WriteType(TargetType);
                writer.Write(')');
                writer.WriteIdentifier(ParameterName);
                break;
            case CastKind.Implicit:
                writer.WriteIdentifier(ParameterName);
                break;
            case CastKind.UnsafeAs:
                writer.Using("System.Runtime.CompilerServices"); // Unsafe
                writer.Write("Unsafe.As<bool, byte>(ref ");
                writer.WriteIdentifier(ParameterName);
                writer.Write(')');
                break;
            default:
                throw new InvalidOperationException("Cast kind is invalid!");
        }
    }
}
