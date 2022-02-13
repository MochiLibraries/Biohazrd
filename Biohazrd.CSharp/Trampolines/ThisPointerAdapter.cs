using System;

namespace Biohazrd.CSharp.Trampolines;

public sealed class ThisPointerAdapter : Adapter
{
    public ThisPointerAdapter(Adapter target)
        : base(target)
    {
        if (target.SpecialKind != SpecialAdapterKind.ThisPointer)
        { throw new ArgumentException("The target adapter is not for the this pointer!", nameof(target)); }

        if (target.InputType is not PointerTypeReference)
        { throw new ArgumentException("The target adapter is not a pointer!", nameof(target)); }

        // This adapter eliminates the explicit this pointer parameter
        AcceptsInput = false;
    }

    public override void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    public override bool WriteBlockBeforeCall(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.Write("fixed (");
        context.WriteType(InputType);
        writer.Write(' ');
        writer.WriteIdentifier(Name);
        writer.WriteLine(" = &this)");
        return true;
    }

    public override void WriteOutputArgument(TrampolineContext context, CSharpCodeWriter writer)
        => writer.WriteIdentifier(Name);
}
