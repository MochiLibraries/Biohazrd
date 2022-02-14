﻿using System;
using System.Diagnostics;

namespace Biohazrd.CSharp.Trampolines;

public sealed class ByRefAdapter : Adapter
{
    public override bool CanEmitDefaultValue => Kind == ByRefKind.In;

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

        OutputType = pointerType;
        InputType = new ByRefTypeReference(kind, pointerType.Inner);
        ParameterName = target.ParameterName;
        TemporaryName = $"__{ParameterName}P";
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