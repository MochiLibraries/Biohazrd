using System;
using System.Diagnostics;

namespace Biohazrd.CSharp.Trampolines;

public abstract class SyntheticAdapter : Adapter
{
    protected SyntheticAdapter(TypeReference inputType, string parameterName)
        : base(inputType, parameterName)
        => Debug.Assert(!ProvidesOutput);

    protected SyntheticAdapter(string parameterName)
        : this(VoidTypeReference.Instance, parameterName)
    { }

    public override sealed void WriteOutputArgument(TrampolineContext context, CSharpCodeWriter writer)
        => throw new InvalidOperationException("Synthetic adapters do not provide an output argument.");
}
