using Biohazrd.Metadata;
using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Common;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class AnonymousTypeTests : BiohazrdTestBase
    {
        [Fact]
        public void TestAnonymousClass()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class ClassA
{
public:
    int BeforeField;
    class
    {
        int FieldInAnonymousClass;
    } AnonymousClassField;
    int AfterField;
};"
            );

            Assert.Single(library.Declarations);

            TranslatedRecord classA = library.FindDeclaration<TranslatedRecord>("ClassA");
            TranslatedRecord anonymousClass = classA.FindDeclaration<TranslatedRecord>();
            TranslatedNormalField anonymousClassField = classA.FindDeclaration<TranslatedNormalField>("AnonymousClassField");
            TranslatedNormalField fieldInAnonymousClass = anonymousClass.FindDeclaration<TranslatedNormalField>("FieldInAnonymousClass");

            Assert.False(classA.IsUnnamed);
            Assert.True(anonymousClass.IsUnnamed);
            Assert.False(classA.Metadata.Has<LazilyGenerated>());
            Assert.True(anonymousClass.Metadata.Has<LazilyGenerated>());
            Assert.Equal(0, fieldInAnonymousClass.Offset);
            Assert.Equal(4, anonymousClassField.Offset);
            Assert.Equal(8, classA.FindDeclaration<TranslatedNormalField>("AfterField").Offset);

            // Reduce types and ensure that AnonymousClassField uses the anonymous class
            // (Doing this without type reduction is possible, but a bit of a pain.)
            TranslatedLibrary reduced = new TypeReductionTransformation().Transform(library);
            TranslatedNormalField reducedAnonymousClassField = reduced.FindDeclaration<TranslatedRecord>("ClassA").FindDeclaration<TranslatedNormalField>("AnonymousClassField");
            Assert.ReferenceEqual(reducedAnonymousClassField.Original, anonymousClassField);

            TranslatedTypeReference anonymousClassFieldTypeReference = (TranslatedTypeReference)reducedAnonymousClassField.Type;
            TranslatedDeclaration? resolvedAnonymousClassFieldType = anonymousClassFieldTypeReference.TryResolve(library);
            Assert.NotNull(resolvedAnonymousClassFieldType);
            Assert.ReferenceEqual(anonymousClass, resolvedAnonymousClassFieldType.Original);
        }
    }
}
