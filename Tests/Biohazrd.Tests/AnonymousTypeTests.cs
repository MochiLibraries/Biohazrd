using Biohazrd.Metadata;
using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Common;
using System.Linq;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class AnonymousTypeTests : BiohazrdTestBase
    {
        private void TestAnonymousType(string cppCode, bool hasNamedField)
        {
            TranslatedLibrary library = CreateLibrary(cppCode);

            TranslatedRecord parentClass = library.FindDeclaration<TranslatedRecord>("TestClass");
            Assert.Empty(parentClass.UnsupportedMembers); // Ensure the backing field when hasNamedField == false is not marked as unsupported
            Assert.Empty(parentClass.OfType<TranslatedUnsupportedDeclaration>()); // Ensure explicit field wasn't double-processed
            Assert.False(parentClass.IsUnnamed);
            Assert.False(parentClass.Metadata.Has<LazilyGenerated>());

            TranslatedRecord anonymousType = parentClass.FindDeclaration<TranslatedRecord>();
            TranslatedNormalField fieldInAnonymous = anonymousType.FindDeclaration<TranslatedNormalField>("FieldInAnonymous");

            TranslatedNormalField fieldOfAnonymous;

            if (hasNamedField)
            { fieldOfAnonymous = parentClass.FindDeclaration<TranslatedNormalField>("FieldOfAnonymous"); }
            else
            { fieldOfAnonymous = parentClass.Members.OfType<TranslatedNormalField>().First(f => f.IsUnnamed); }

            Assert.True(anonymousType.IsUnnamed);
            Assert.True(anonymousType.Metadata.Has<LazilyGenerated>());
            Assert.Equal(0, fieldInAnonymous.Offset);
            Assert.Equal(4, fieldOfAnonymous.Offset);
            Assert.Equal(8, parentClass.FindDeclaration<TranslatedNormalField>("AfterField").Offset);

            // Reduce types and ensure that FieldOfAnonymous uses the anonymous type
            TranslatedLibrary reduced = new TypeReductionTransformation().Transform(library);
            TranslatedNormalField reducedFieldOfAnonymous = reduced.FindDeclaration<TranslatedRecord>("TestClass").FindDeclaration<TranslatedNormalField>(f => f.Original == fieldOfAnonymous.Original);

            TranslatedTypeReference fieldOfAnonymousTypeReference = (TranslatedTypeReference)reducedFieldOfAnonymous.Type;
            TranslatedDeclaration? resolvedAnonymousClassFieldType = fieldOfAnonymousTypeReference.TryResolve(library);
            Assert.NotNull(resolvedAnonymousClassFieldType);
            Assert.ReferenceEqual(anonymousType, resolvedAnonymousClassFieldType.Original);
        }

        private void TestAnonymousTypeWithField(string cppCode)
            => TestAnonymousType(cppCode, hasNamedField: true);

        private void TestAnonymousTypeWithoutField(string cppCode)
            => TestAnonymousType(cppCode, hasNamedField: false);

        [Fact]
        public void TestAnonymousClassWithField()
        {
            TestAnonymousTypeWithField
            (@"
class TestClass
{
public:
    int BeforeField;
    class
    {
public:
        int FieldInAnonymous;
    } FieldOfAnonymous;
    int AfterField;
};"
            );
        }

        [Fact]
        public void TestAnonymousStructWithField()
        {
            TestAnonymousTypeWithField
(@"
class TestClass
{
public:
    int BeforeField;
    struct
    {
        int FieldInAnonymous;
    } FieldOfAnonymous;
    int AfterField;
};"
);
        }

        [Fact]
        public void TestAnonymousUnionWithField()
        {
            TestAnonymousTypeWithField
(@"
class TestClass
{
public:
    int BeforeField;
    union
    {
        int FieldInAnonymous;
    } FieldOfAnonymous;
    int AfterField;
};"
);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/116")]
        public void TestAnonymousClassWithoutField()
        {
            TestAnonymousTypeWithoutField
            (@"
class TestClass
{
public:
    int BeforeField;
    class
    {
public:
        int FieldInAnonymous;
    };
    int AfterField;
};"
            );
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/116")]
        public void TestAnonymousStructWithoutField()
        {
            TestAnonymousTypeWithoutField
(@"
class TestClass
{
public:
    int BeforeField;
    struct
    {
        int FieldInAnonymous;
    };
    int AfterField;
};"
);
        }

        [Fact]
        public void TestAnonymousUnionWithoutField()
        {
            TestAnonymousTypeWithoutField
(@"
class TestClass
{
public:
    int BeforeField;
    union
    {
        int FieldInAnonymous;
    };
    int AfterField;
};"
);
        }
    }
}
