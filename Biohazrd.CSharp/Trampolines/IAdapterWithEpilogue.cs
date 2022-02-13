namespace Biohazrd.CSharp.Trampolines;

public interface IAdapterWithEpilogue
{
    void WriteEpilogue(TrampolineContext context, CSharpCodeWriter writer);
}
