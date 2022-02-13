using Biohazrd.CSharp.Infrastructure;
using Biohazrd.Expressions;

namespace Biohazrd.CSharp.Trampolines;

public struct TrampolineContext
{
    private ICSharpOutputGenerator OutputGenerator { get; }
    public Trampoline Target { get; }
    public CSharpCodeWriter Writer { get; }

    internal VisitorContext Context { get; init; }
    internal TranslatedDeclaration Declaration { get; set; }

    internal TrampolineContext(ICSharpOutputGenerator outputGenerator, Trampoline target, CSharpCodeWriter writer, VisitorContext context, TranslatedFunction declaration)
    {
        OutputGenerator = outputGenerator;
        Target = target;
        Writer = writer;
        Context = context;
        Declaration = declaration;
    }

    public CSharpGenerationOptions Options => OutputGenerator.Options;

    public string GetTypeAsString(TypeReference type)
        => OutputGenerator.GetTypeAsString(Context, Declaration, type);

    public void WriteType(TypeReference type)
        => Writer.Write(GetTypeAsString(type));

    public string GetConstantAsString(ConstantValue constant, TypeReference targetType)
        => OutputGenerator.GetConstantAsString(Context, Declaration, constant, targetType);

    public void WriteConstant(ConstantValue constant, TypeReference targetType)
        => Writer.Write(GetConstantAsString(constant, targetType));
}
