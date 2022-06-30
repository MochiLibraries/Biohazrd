namespace Biohazrd.BoilerplateGenerator;

internal static class WellKnown
{
    // BCL symbol names
    public const string SystemCollectionsImmutable = "System.Collections.Immutable";
    public const string ImmutableArray = nameof(ImmutableArray);
    public const string ImmutableList = nameof(ImmutableList);

    // Biohazrd namespaces
    public const string Biohazrd = nameof(Biohazrd);
    public const string BiohazrdTransformation = $"{nameof(Biohazrd)}.Transformation";
    public const string BiohazrdTransformationCommon = $"{BiohazrdTransformation}.Common";
    public const string BiohazrdTransformationInfrastructure = $"{BiohazrdTransformation}.Infrastructure";

    // Biohazrd attributes affecting source generation
    public const string CatchAllMembersPropertyAttribute = nameof(CatchAllMembersPropertyAttribute);
    public const string DoNotGenerateBoilerplateMethodsAttribute = nameof(DoNotGenerateBoilerplateMethodsAttribute);

    // Well known Biohazrd types
    public const string TranslatedDeclaration = nameof(TranslatedDeclaration);
    public const string TypeReference = nameof(TypeReference);
    public const string TranslatedDeclarationFullName = $"{Biohazrd}.{TranslatedDeclaration}";
    public const string TypeReferenceFullName = $"{Biohazrd}.{TypeReference}";

    // Types we generate
    public const string DeclarationVisitor = nameof(DeclarationVisitor);
    public const string RawTransformationBase = nameof(RawTransformationBase);
    public const string TransformationBase = nameof(TransformationBase);
    public const string RawTypeTransformationBase = nameof(RawTypeTransformationBase);
    public const string TypeTransformationBase = nameof(TypeTransformationBase);
    public const string SimpleTransformation = nameof(SimpleTransformation);

    // Select methods we call in generated code
    public const string VisitUnknownDeclarationType = nameof(VisitUnknownDeclarationType);
    public const string TransformUnknownDeclarationType = nameof(TransformUnknownDeclarationType);
    public const string TransformUnknownTypeReference = nameof(TransformUnknownTypeReference);
}
