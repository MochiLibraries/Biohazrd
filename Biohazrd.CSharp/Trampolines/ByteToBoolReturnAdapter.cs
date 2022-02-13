using System;

namespace Biohazrd.CSharp.Trampolines;
public sealed class ByteToBoolReturnAdapter : IReturnAdapter
{
    public TypeReference OutputType => CSharpBuiltinType.Bool;
    public string TemporaryName => "__result";
    public bool UsePedanticConversion { get; set; } = false;

    public ByteToBoolReturnAdapter(IReturnAdapter target)
    {
        if (target.OutputType != CSharpBuiltinType.Byte)
        { throw new ArgumentException("The target adapter does not produce a byte!", nameof(target)); }
    }

    public void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    {
        context.WriteType(CSharpBuiltinType.Byte);
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

        if (UsePedanticConversion)
        {
            writer.WriteIdentifier(TemporaryName);
            writer.WriteLine("!= 0;");
        }
        else
        {
            writer.Using("System.Runtime.CompilerServices"); // Unsafe
            writer.Write("Unsafe.As<byte, bool>(ref ");
            writer.WriteIdentifier(TemporaryName);
            writer.WriteLine(");");
        }
    }
}
