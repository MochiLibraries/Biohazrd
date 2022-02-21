namespace Biohazrd.CSharp.Trampolines;

public interface IAdapterWithGenericParameter
{
    void WriteGenericParameter(TrampolineContext context, CSharpCodeWriter writer);
    void WriteGenericConstraint(TrampolineContext context, CSharpCodeWriter writer);
}
