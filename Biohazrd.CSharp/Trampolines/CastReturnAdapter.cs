using System;

namespace Biohazrd.CSharp.Trampolines;

public sealed class CastReturnAdapter : IReturnAdapter, IShortReturnAdapter
{
    public TypeReference OutputType { get; }
    public TypeReference SourceType { get; }
    public CastKind Kind { get; }
    public string TemporaryName => "__result";

    public CastReturnAdapter(IReturnAdapter target, TypeReference outputType, CastKind kind)
    {
        if (!Enum.IsDefined(kind))
        { throw new ArgumentOutOfRangeException(nameof(kind)); }

        OutputType = outputType;
        SourceType = target.OutputType;
        Kind = kind;
    }

    bool IShortReturnAdapter.CanEmitShortReturn => Kind is CastKind.Implicit or CastKind.Explicit;

    void IShortReturnAdapter.WriteShortPrologue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    void IShortReturnAdapter.WriteShortReturn(TrampolineContext context, CSharpCodeWriter writer)
    {
        switch (Kind)
        {
            case CastKind.Implicit:
                writer.Write("return ");
                break;
            case CastKind.Explicit:
                writer.Write("return (");
                context.WriteType(OutputType);
                writer.Write(')');
                break;
            case CastKind.UnsafeAs:
                throw new InvalidOperationException("UnsafeAs casts cannot be short returns!");
            default:
                throw new InvalidOperationException("Cast kind is invalid!");
        }
    }

    public void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    {
        context.WriteType(SourceType);
        writer.Write(' ');
        writer.WriteIdentifier(TemporaryName);
        writer.WriteLine(';');
    }

    public void WriteResultCapture(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.WriteIdentifier(TemporaryName);
        writer.Write(" = ");
    }

    public void WriteEpilogue(TrampolineContext context, CSharpCodeWriter writer)
    {
        switch (Kind)
        {
            case CastKind.Implicit:
                writer.Write("return ");
                writer.WriteIdentifier(TemporaryName);
                break;
            case CastKind.Explicit:
                writer.Write("return (");
                context.WriteType(OutputType);
                writer.Write(')');
                writer.WriteIdentifier(TemporaryName);
                break;
            case CastKind.UnsafeAs:
                writer.Using("System.Runtime.CompilerServices"); // Unsafe
                writer.Write("return Unsafe.As<byte, bool>(ref ");
                writer.WriteIdentifier(TemporaryName);
                writer.WriteLine(");");
                break;
            default:
                throw new InvalidOperationException("Cast kind is invalid!");
        }
    }
}
