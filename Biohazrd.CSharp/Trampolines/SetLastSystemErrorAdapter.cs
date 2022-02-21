using System.Diagnostics;

namespace Biohazrd.CSharp.Trampolines;

public sealed class SetLastSystemErrorAdapter : SyntheticAdapter, IAdapterWithInnerWrapper
{
    public override bool CanEmitDefaultValue => false;

    /// <summary>Skips clearing the last system error before the P/Invoke</summary>
    /// <remarks>
    /// Leaving this as <c>false</c> will clear the last system error before invoking the native function.
    /// This matches the behavior of the built-in .NET marshaler but is generally not needed.
    /// </remarks>
    public bool SkipPedanticClear { get; }

    public SetLastSystemErrorAdapter(bool skipPedanticClear)
        : base("__SetLastError")
    {
        SkipPedanticClear = skipPedanticClear;
        AcceptsInput = false;
    }

    public override void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    public override bool WriteBlockBeforeCall(TrampolineContext context, CSharpCodeWriter writer)
        => false;

    void IAdapterWithInnerWrapper.WriterInnerPrologue(TrampolineContext context, CSharpCodeWriter writer)
    {
        Debug.Assert(context.Options.TargetRuntime >= TargetRuntime.Net6, "The APIs used by this adapter require .NET 6 or later.");

        if (!SkipPedanticClear)
        {
            writer.Using("System.Runtime.InteropServices"); // Marshal
            writer.WriteLine("Marshal.SetLastSystemError(0);");
        }
    }

    void IAdapterWithInnerWrapper.WriteInnerEpilogue(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.Using("System.Runtime.InteropServices"); // Marshal
        writer.WriteLine("Marshal.SetLastPInvokeError(Marshal.GetLastSystemError());");
    }
}
