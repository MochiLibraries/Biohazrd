//#define SANITY_CHECK_RETURNED_POINTER
using System;

namespace Biohazrd.CSharp.Trampolines;

public sealed class ReturnByReferenceAdapter : Adapter, IReturnAdapter
{
    public TypeReference OutputType { get; }
    public override bool CanEmitDefaultValue => false;

    public string TemporaryName => "__returnBuffer";
#if SANITY_CHECK_RETURNED_POINTER
    public string SanityCheckName => "__returnBuffer2";
#endif

    internal ReturnByReferenceAdapter(Adapter returnBufferParameter)
        : base(returnBufferParameter)
    {
        if (returnBufferParameter.SpecialKind != SpecialAdapterKind.ReturnBuffer)
        { throw new ArgumentException("The specified return buffer adapter target is not a return buffer!", nameof(returnBufferParameter)); }

        if (returnBufferParameter.InputType is not PointerTypeReference returnBufferPointerType)
        { throw new ArgumentException("The specified return buffer parameter is not a pointer.", nameof(returnBufferParameter)); }

        OutputType = returnBufferPointerType.Inner;

        // This adapter eliminates the return buffer parameter
        AcceptsInput = false;
    }

    public override void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    void IReturnAdapter.WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    {
        context.WriteType(OutputType);
        writer.Write(' ');
        writer.WriteIdentifier(TemporaryName);
        writer.WriteLine(';');
#if SANITY_CHECK_RETURNED_POINTER
        context.WriteType(InputType);
        writer.Write(' ');
        writer.WriteIdentifier(SanityCheckName);
        writer.WriteLine(';');
#endif
    }

    public void WriteResultCapture(TrampolineContext context, CSharpCodeWriter writer)
    {
#if SANITY_CHECK_RETURNED_POINTER
        writer.WriteIdentifier(SanityCheckName);
        writer.Write(" = ");
#endif
    }

    public void WriteEpilogue(TrampolineContext context, CSharpCodeWriter writer)
    {
#if SANITY_CHECK_RETURNED_POINTER
        writer.Using("System.Diagnostics"); // Debug
        writer.Write("Debug.Assert(");
        writer.WriteIdentifier(SanityCheckName);
        writer.Write(" == &");
        writer.WriteIdentifier(TemporaryName);
        writer.WriteLine(");");
#endif
        writer.Write("return ");
        writer.WriteIdentifier(TemporaryName);
        writer.WriteLine(';');
    }

    public override void WriteInputParameter(TrampolineContext context, CSharpCodeWriter writer, bool emitDefaultValue)
        => throw new InvalidOperationException("This adapter does not accept input.");

    public override bool WriteBlockBeforeCall(TrampolineContext context, CSharpCodeWriter writer)
        => false;

    public override void WriteOutputArgument(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.Write('&');
        writer.WriteIdentifier(TemporaryName);
    }
}
