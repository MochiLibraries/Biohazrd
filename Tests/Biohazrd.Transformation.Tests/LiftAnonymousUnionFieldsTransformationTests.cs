using Biohazrd.CSharp;
using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Common;
using Xunit;

namespace Biohazrd.Transformation.Tests
{
    public sealed class LiftAnonymousUnionFieldsTransformationTests : BiohazrdTestBase
    {
        private void AssertNoUnions(TranslatedLibrary library)
        {
            int unionCount = 0;
            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (declaration is TranslatedRecord { Kind: RecordKind.Union })
                { unionCount++; }
            }

            Assert.Equal(0, unionCount);
        }

        [Fact]
        public void BasicAnonymousUnion1()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct Test
{
    union
    {
        int FieldA;
        float FieldB;
    };
    int After;
};
"
            );

            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new LiftAnonymousUnionFieldsTransformation().Transform(library);

            AssertNoUnions(library);
            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("FieldA").Offset);
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("FieldB").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);
        }

        [Fact]
        public void BasicAnonymousUnion2()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct Test
{
    int Before;
    union
    {
        int FieldA;
        float FieldB;
    };
    int After;
};
"
            );

            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new LiftAnonymousUnionFieldsTransformation().Transform(library);

            AssertNoUnions(library);
            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("Before").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldA").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldB").Offset);
            Assert.Equal(8, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);
        }

        [Fact]
        public void DoNotLiftAnonyomousUnionWithNamedField()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct Test
{
    int Before;
    union
    {
        int FieldA;
        float FieldB;
    } UnionField;
    int After;
};
"
            );

            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new LiftAnonymousUnionFieldsTransformation().Transform(library);

            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            TranslatedRecord union = testStruct.FindDeclaration<TranslatedRecord>();
            Assert.True(union.IsUnnamed);
            Assert.Equal(RecordKind.Union, union.Kind);

            TranslatedNormalField unionField = testStruct.FindDeclaration<TranslatedNormalField>("UnionField");

            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("Before").Offset);
            Assert.Equal(4, unionField.Offset);
            Assert.Equal(8, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);

            Assert.Equal(0, union.FindDeclaration<TranslatedNormalField>("FieldA").Offset);
            Assert.Equal(0, union.FindDeclaration<TranslatedNormalField>("FieldB").Offset);

            TranslatedTypeReference unionFieldTypeReference = (TranslatedTypeReference)unionField.Type;
            Assert.ReferenceEqual(union, unionFieldTypeReference.TryResolve(library));
        }

        [FutureFact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/59")]
        public void NestedUnion()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct Test
{
    int Before;
    union
    {
        int FieldA;
        float FieldB;
        union
        {
            short FieldC;
            char FieldD;
        };
    };
    int After;
};
"
            );

            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new LiftAnonymousUnionFieldsTransformation().Transform(library);

            AssertNoUnions(library);
            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("Before").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldA").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldB").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldC").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldD").Offset);
            Assert.Equal(8, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);
        }

        [FutureFact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/117")]
        public void UnionInStructInUnionInStruct()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct Test
{
    int Before;
    union
    {
        struct
        {
            union
            {
                int Field;
            };
        };
    };
    int After;
};
"
            );

            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new LiftAnonymousUnionFieldsTransformation().Transform(library);

            AssertNoUnions(library);
            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("Before").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("Field").Offset);
            Assert.Equal(8, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);
        }
    }
}
