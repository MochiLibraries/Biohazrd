using System;

namespace Biohazrd.CSharp.Trampolines;

public sealed class VoidReturnAdapter : IReturnAdapter, IShortReturnAdapter
{
    public TypeReference OutputType => VoidTypeReference.Instance;

    internal static readonly VoidReturnAdapter Instance = new();

    private VoidReturnAdapter()
    { }

    public static VoidReturnAdapter Create(IReturnAdapter target)
    {
        if (target.OutputType is not VoidTypeReference)
        { throw new ArgumentException("The specified adapter does not return void!", nameof(target)); }

        return Instance;
    }

    public void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    public void WriteResultCapture(TrampolineContext context, CSharpCodeWriter writer)
    { }

    public void WriteEpilogue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    public void WriteShortPrologue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    public void WriteShortReturn(TrampolineContext context, CSharpCodeWriter writer)
    { }
}
