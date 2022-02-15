using System;

namespace Biohazrd.CSharp.Trampolines;

public sealed class ByRefReturnAdapter : IReturnAdapter, IShortReturnAdapter
{
    private readonly TypeReference TargetOutputType;
    public TypeReference OutputType { get; }
    public string TemporaryName => "__result";
    public ByRefKind Kind { get; }

    public ByRefReturnAdapter(IReturnAdapter target, ByRefKind kind)
    {
        if (kind != ByRefKind.Ref && kind != ByRefKind.RefReadOnly)
        {
            if (kind == ByRefKind.Out)
            { throw new ArgumentException("Returns cannot be out byrefs.", nameof(kind)); }
            else
            { throw new ArgumentOutOfRangeException(nameof(kind)); }
        }

        if (target.OutputType is not PointerTypeReference pointerType)
        { throw new ArgumentException("The target of this adapter must return a pointer.", nameof(target.OutputType)); }

        Kind = kind;
        OutputType = new ByRefTypeReference(Kind, pointerType.Inner);
        TargetOutputType = pointerType;
    }

    void IShortReturnAdapter.WriteShortPrologue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    void IShortReturnAdapter.WriteShortReturn(TrampolineContext context, CSharpCodeWriter writer)
        => writer.Write("return ref *");

    void IReturnAdapter.WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    {
        // Ref locals must be assigned upon declaration in C#, so we keep things as a pointer and conver to a reference at the very end.
        context.WriteType(TargetOutputType);
        writer.Write(' ');
        writer.WriteIdentifier(TemporaryName);
        writer.WriteLine(';');
    }

    void IReturnAdapter.WriteResultCapture(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.WriteIdentifier(TemporaryName);
        writer.Write(" = ");
    }

    void IReturnAdapter.WriteEpilogue(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.Write("return ref *");
        writer.WriteIdentifier(TemporaryName);
        writer.WriteLine(';');
    }
}
