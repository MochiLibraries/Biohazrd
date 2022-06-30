using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Threading;

namespace Biohazrd.BoilerplateGenerator;

internal sealed class ExternalSymbolsVisitor : SymbolVisitor
{
    private readonly INamedTypeSymbol? TranslatedDeclarationSymbol;
    private readonly INamedTypeSymbol? TypeReferenceSymbol;

    private readonly ImmutableArray<TranslatedDeclarationInfo>.Builder Declarations = ImmutableArray.CreateBuilder<TranslatedDeclarationInfo>();
    private readonly ImmutableArray<TypeReferenceInfo>.Builder TypeReferences = ImmutableArray.CreateBuilder<TypeReferenceInfo>();
    private readonly SymbolEqualityComparer SymbolComparer = SymbolEqualityComparer.Default;

    private readonly CancellationToken CancellationToken;

    public ExternalSymbolsVisitor(IAssemblySymbol? assembly, CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;

        TranslatedDeclarationSymbol = assembly?.GetTypeByMetadataName(WellKnown.TranslatedDeclarationFullName);
        TypeReferenceSymbol = assembly?.GetTypeByMetadataName(WellKnown.TypeReferenceFullName);

        if (TranslatedDeclarationSymbol is not null || TypeReferenceSymbol is not null)
        { VisitAssembly(assembly!); }
    }

    public (ImmutableArray<TranslatedDeclarationInfo> Declarations, ImmutableArray<TypeReferenceInfo> TypeReferences) GetResults()
        => (Declarations.MoveToImmutableSafe(), TypeReferences.MoveToImmutableSafe());

    public override void DefaultVisit(ISymbol symbol)
        => CancellationToken.ThrowIfCancellationRequested();

    public override void VisitAssembly(IAssemblySymbol symbol)
    {
        base.VisitAssembly(symbol);
        symbol.GlobalNamespace.Accept(this);
    }

    public override void VisitNamespace(INamespaceSymbol symbol)
    {
        base.VisitNamespace(symbol);

        foreach (INamespaceOrTypeSymbol member in symbol.GetMembers())
        { member.Accept(this); }
    }

    public override void VisitNamedType(INamedTypeSymbol symbol)
    {
        base.VisitNamedType(symbol);

        foreach (ITypeSymbol member in symbol.GetTypeMembers())
        { member.Accept(this); }

        // Skip non-public types since we won't be able to refer to them
        if (symbol.DeclaredAccessibility != Accessibility.Public)
        { return; }

        // Skip explicitly ignored types
        if (symbol.ShouldIgnore())
        { return; }

        // Determine if this type derives from TranslatedDeclaration or TypeReference
        bool derivesFromTranslatedDeclaration = false;
        bool derivesFromTypeReference = false;
        int distanceFromBase = 0;

        for (ITypeSymbol? baseType = symbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            CancellationToken.ThrowIfCancellationRequested();

            distanceFromBase++;

            if (SymbolComparer.Equals(baseType, TranslatedDeclarationSymbol))
            {
                derivesFromTranslatedDeclaration = true;
                break;
            }

            if (SymbolComparer.Equals(baseType, TypeReferenceSymbol))
            {
                derivesFromTypeReference = true;
                break;
            }
        }

        // Collect the relevant info from the type
        if (derivesFromTranslatedDeclaration)
        { Declarations.Add(new TranslatedDeclarationInfo(symbol, distanceFromBase, isExternal: true)); }
        else if (derivesFromTypeReference)
        { TypeReferences.Add(new TypeReferenceInfo(symbol, distanceFromBase, isExternal: true)); }
    }
}
