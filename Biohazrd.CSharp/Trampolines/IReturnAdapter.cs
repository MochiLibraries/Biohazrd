namespace Biohazrd.CSharp.Trampolines;

public interface IReturnAdapter
{
    TypeReference OutputType { get; }

    void WriteReturnType(TrampolineContext context, CSharpCodeWriter writer)
        => context.WriteType(OutputType);

    void WritePrologue(TrampolineContext context, CSharpCodeWriter writer);
    void WriteResultCapture(TrampolineContext context, CSharpCodeWriter writer);
    void WriteEpilogue(TrampolineContext context, CSharpCodeWriter writer);
}
