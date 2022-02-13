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

        if (target is IAdapterWithGenericParameter)
        { throw new NotSupportedException("Adapting to a generic return type is not supported."); }

        OutputType = target.OutputType;
    }

    public void WriteShortPrologue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    public void WriteShortReturn(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.Write("return ");

        if (OutputType is ByRefTypeReference)
        { writer.Write("ref "); }
    }

    public void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    {
        context.WriteType(OutputType);
        writer.Write(' ');
        writer.WriteIdentifier(TemporaryName);

        // Ref locals cannot be declared without an initializer
        if (OutputType is ByRefTypeReference byRefType)
        {
            writer.Using("System.Runtime.CompilerServices"); // Unsafe
            writer.Write("ref Unsafe.NullRef<");
            context.WriteType(byRefType.Inner);
            writer.Write(">()");
        }

        writer.WriteLine(';');
    }

    public void WriteResultCapture(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.WriteIdentifier(TemporaryName);
        writer.Write(" = ");

        if (OutputType is ByRefTypeReference)
        { writer.Write("ref "); }
    }

    public void WriteEpilogue(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.Write("return ");

        if (OutputType is ByRefTypeReference)
        { writer.Write("ref "); }

        writer.WriteIdentifier(TemporaryName);
        writer.WriteLine(';');
    }
}
