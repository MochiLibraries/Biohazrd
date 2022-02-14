using Biohazrd.CSharp.Infrastructure;
using System;
using System.Diagnostics;

namespace Biohazrd.CSharp.Trampolines;

public sealed class NonBlittableTypeReturnAdapter : IReturnAdapter
{
    internal NonBlittableTypeKind Kind { get; }
    public TypeReference OutputType { get; }
    private string TemporaryName => "__result";

    private NonBlittableTypeReturnAdapter(NonBlittableTypeKind kind)
    {
        Kind = kind;
        OutputType = kind switch
        {
            // Fib about the type we return, the actual types provide implicit conversions to the expected ones
            NonBlittableTypeKind.NativeBoolean => CSharpBuiltinType.Bool,
            NonBlittableTypeKind.NativeChar => CSharpBuiltinType.Char,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    internal static readonly NonBlittableTypeReturnAdapter NativeBoolean = new(NonBlittableTypeKind.NativeBoolean);
    internal static readonly NonBlittableTypeReturnAdapter NativeChar = new(NonBlittableTypeKind.NativeChar);

    void IReturnAdapter.WriteReturnType(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.Using(context.Options.InfrastructureTypesNamespace);
        (context.OutputGenerator as ICSharpOutputGeneratorInternal)?.__IndicateInfrastructureTypeDependency(Kind);
        writer.Write(Kind.ToString());
    }

    void IReturnAdapter.WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    {
        Debug.Fail("This adapter should not be used in a context where it is emitted in a function body.");
        context.WriteType(OutputType);
        writer.Write(' ');
        writer.WriteIdentifier(TemporaryName);
        writer.WriteLine(';');
    }

    void IReturnAdapter.WriteResultCapture(TrampolineContext context, CSharpCodeWriter writer)
    {
        Debug.Fail("This adapter should not be used in a context where it is emitted in a function body.");
        writer.WriteIdentifier(TemporaryName);
        writer.Write(" = ");
    }

    void IReturnAdapter.WriteEpilogue(TrampolineContext context, CSharpCodeWriter writer)
    {
        Debug.Fail("This adapter should not be used in a context where it is emitted in a function body.");
        writer.Write("return ");
        writer.WriteIdentifier(TemporaryName);
        writer.WriteLine(';');
    }
}
