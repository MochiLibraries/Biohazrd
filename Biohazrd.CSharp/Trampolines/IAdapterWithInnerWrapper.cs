namespace Biohazrd.CSharp.Trampolines;

public interface IAdapterWithInnerWrapper
{
    void WriterInnerPrologue(TrampolineContext context, CSharpCodeWriter writer);
    void WriteInnerEpilogue(TrampolineContext context, CSharpCodeWriter writer);
}
