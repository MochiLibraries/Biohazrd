using System;

namespace Biohazrd.CSharp.Trampolines;

public sealed class ToPointerAdapter : Adapter
{
    public ToPointerAdapter(Adapter target)
        : base(target)
    {
        if (!target.AcceptsInput)
        { throw new ArgumentException("The target adapter does not accept an input!", nameof(target)); }

        if (target.InputType is not PointerTypeReference pointerType)
        { throw new ArgumentException("By ref adapters must target pointers!", nameof(target)); }

        InputType = pointerType.Inner;
        ParameterName = target.ParameterName;
    }

    public override void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    public override bool WriteBlockBeforeCall(TrampolineContext context, CSharpCodeWriter writer)
        => false;

    public override void WriteOutputArgument(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.Write('&');
        writer.WriteIdentifier(ParameterName);
    }
}
