namespace Biohazrd.CSharp.Infrastructure;

internal interface ICSharpOutputGeneratorInternal : ICSharpOutputGenerator
{
    void Fatal(VisitorContext context, TranslatedDeclaration declaration, string? reason, string? extraDescription);
    void Fatal(VisitorContext context, TranslatedDeclaration declaration, string? reason);

    // This is used for a dirty hack to work around our lack of proper support for late-generate infrastructure types
    void __IndicateInfrastructureTypeDependency(NonBlittableTypeKind kind);
}
