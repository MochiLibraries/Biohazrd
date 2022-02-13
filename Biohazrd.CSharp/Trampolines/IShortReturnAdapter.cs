namespace Biohazrd.CSharp.Trampolines;

public interface IShortReturnAdapter : IReturnAdapter
{
    void WriteShortPrologue(TrampolineContext context, CSharpCodeWriter writer);
    void WriteShortReturn(TrampolineContext context, CSharpCodeWriter writer);
}
