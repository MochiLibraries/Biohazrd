using Biohazrd.CSharp.Infrastructure;
using System;
using System.Diagnostics;

namespace Biohazrd.CSharp.Trampolines;

public sealed class NonBlittableTypeAdapter : Adapter
{
    internal NonBlittableTypeKind Kind { get; }
    public override bool CanEmitDefaultValue => false;

    internal NonBlittableTypeAdapter(TranslatedParameter target, NonBlittableTypeKind kind)
        : base(target)
    {
        if (!Enum.IsDefined(kind))
        { throw new ArgumentOutOfRangeException(nameof(kind)); }

        Kind = kind;
        InputType = kind switch
        {
            // Fib about the type we take, the actual types provide implicit conversions from the expected ones
            NonBlittableTypeKind.NativeBoolean => CSharpBuiltinType.Bool,
            NonBlittableTypeKind.NativeChar => CSharpBuiltinType.Char,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    public override void WriteInputType(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.Using(context.Options.InfrastructureTypesNamespace);
        (context.OutputGenerator as ICSharpOutputGeneratorInternal)?.__IndicateInfrastructureTypeDependency(Kind);
        writer.Write(Kind.ToString());
    }

    public override void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
        => Debug.Fail("This adapter should not be used in a context where it is emitted in a function body.");

    public override bool WriteBlockBeforeCall(TrampolineContext context, CSharpCodeWriter writer)
    {
        Debug.Fail("This adapter should not be used in a context where it is emitted in a function body.");
        return false;
    }

    public override void WriteOutputArgument(TrampolineContext context, CSharpCodeWriter writer)
    {
        Debug.Fail("This adapter should not be used in a context where it is emitted in a function body.");
        writer.WriteIdentifier(ParameterName);
    }
}
