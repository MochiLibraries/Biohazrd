using System;

namespace Biohazrd.CSharp.Trampolines;

public sealed class PassthroughReturnAdapter : IReturnAdapter, IShortReturnAdapter
{
    public TypeReference OutputType { get; }
    public string TemporaryName => "__result";

    internal PassthroughReturnAdapter(TypeReference type)
    {
        if (type is VoidTypeReference)
        { throw new ArgumentException($"Cannot passthrough void type, use {nameof(VoidReturnAdapter)}.", nameof(type)); }

        OutputType = type;
    }

    public PassthroughReturnAdapter(IReturnAdapter target)
    {
        if (target.OutputType is VoidTypeReference)
        { throw new ArgumentException("The specified target returns void.", nameof(target)); }

        OutputType = target.OutputType;
    }

    public void WriteShortPrologue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    public void WriteShortReturn(TrampolineContext context, CSharpCodeWriter writer)
        => writer.Write("return ");

    public void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    {
        context.WriteType(OutputType);
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
        writer.Write("return ");
        writer.WriteIdentifier(TemporaryName);
        writer.WriteLine(';');
    }
}
