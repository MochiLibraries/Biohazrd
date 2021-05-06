using Biohazrd.CSharp;
using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Common;
using System.Linq;
using Xunit;

namespace Biohazrd.Transformation.Tests
{
    public sealed class LiftAnonymousRecordFieldsTransformationTests : BiohazrdTestBase
    {
        private void AssertAnonymousTypesNotReferenced(TranslatedLibrary library)
        {
            // Strip unreferenced lazy declarations, which will remove the anonymous unions (assuming they aren't referenced.)
            library = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);

            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (declaration is TranslatedRecord)
                { Assert.False(declaration.IsUnnamed); }
            }
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
            library = new LiftAnonymousRecordFieldsTransformation().Transform(library);

            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("FieldA").Offset);
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("FieldB").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);

            AssertAnonymousTypesNotReferenced(library);
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
            library = new LiftAnonymousRecordFieldsTransformation().Transform(library);

            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("Before").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldA").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldB").Offset);
            Assert.Equal(8, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);

            AssertAnonymousTypesNotReferenced(library);
        }

        [Fact]
        public void BasicAnonymousStruct()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct Test
{
    int Before;
    struct
    {
        int FieldA;
        int FieldB;
    };
    int After;
};
"
            );

            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new LiftAnonymousRecordFieldsTransformation().Transform(library);

            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("Before").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldA").Offset);
            Assert.Equal(8, testStruct.FindDeclaration<TranslatedNormalField>("FieldB").Offset);
            Assert.Equal(12, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);

            AssertAnonymousTypesNotReferenced(library);
        }

        [Fact]
        public void BasicAnonymousClass()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct Test
{
    int Before;
    class
    {
    public:
        int FieldA;
        int FieldB;
    };
    int After;
};
"
            );

            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new LiftAnonymousRecordFieldsTransformation().Transform(library);

            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("Before").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldA").Offset);
            Assert.Equal(8, testStruct.FindDeclaration<TranslatedNormalField>("FieldB").Offset);
            Assert.Equal(12, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);

            AssertAnonymousTypesNotReferenced(library);
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
            library = new LiftAnonymousRecordFieldsTransformation().Transform(library);

            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            TranslatedRecord union = testStruct.FindDeclaration<TranslatedRecord>();
            Assert.True(union.IsUnnamed);
            Assert.Equal(RecordKind.Union, union.Kind);

            TranslatedNormalField unionField = testStruct.FindDeclaration<TranslatedNormalField>("UnionField");

            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("Before").Offset);
            Assert.Equal(4, unionField.Offset);
            Assert.Equal(8, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);

            Assert.Empty(testStruct.Members.Where(m => m.Name.StartsWith("Field")));

            Assert.Equal(0, union.FindDeclaration<TranslatedNormalField>("FieldA").Offset);
            Assert.Equal(0, union.FindDeclaration<TranslatedNormalField>("FieldB").Offset);

            TranslatedTypeReference unionFieldTypeReference = (TranslatedTypeReference)unionField.Type;
            Assert.ReferenceEqual(union, unionFieldTypeReference.TryResolve(library));
        }

        [Fact]
        public void AnonymousTypeWithExplicitFieldInsideAnonymousTypeWithout()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct Test
{
    int Before;
    union
    {
        int FieldA;
        union
        {
            int FieldAA;
            short FieldBB;
        } FieldB;
    };
    int After;
};
"
            );

            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new LiftAnonymousRecordFieldsTransformation().Transform(library);

            // Strip the unreferenced types, which should remove the anonymous union without the backing field
            {
                TranslatedLibrary stripped = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);
                Assert.NotReferenceEqual(library, stripped);
                library = stripped;
            }

            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("Before").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldA").Offset);
            TranslatedNormalField fieldB = testStruct.FindDeclaration<TranslatedNormalField>("FieldB");
            Assert.Equal(4, fieldB.Offset);
            Assert.Equal(8, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);

            // Find the anonymous union type
            // We're ensuring there's only one to ensure that the reference from FieldB did not keep the outer union alive.
            TranslatedRecord? anonymousUnion = null;
            foreach (TranslatedRecord record in testStruct.OfType<TranslatedRecord>().Where(r => r.IsUnnamed))
            {
                Assert.Null(anonymousUnion);
                anonymousUnion = record;
            }
            Assert.NotNull(anonymousUnion);

            TranslatedTypeReference fieldBType = Assert.IsAssignableFrom<TranslatedTypeReference>(fieldB.Type);
            Assert.Equal(anonymousUnion, fieldBType.TryResolve(library));
        }

        [Fact]
        public void AnonymousTypeWithTwoExplicitFieldsInsideAnonymousTypeWithout()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct Test
{
    int Before;
    struct
    {
        int FieldA;
        union
        {
            int FieldAA;
            short FieldBB;
        } FieldB, FieldC;
    };
    int After;
};
"
            );

            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new LiftAnonymousRecordFieldsTransformation().Transform(library);

            // Strip the unreferenced types, which should remove the anonymous union without the backing field
            {
                TranslatedLibrary stripped = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);
                Assert.NotReferenceEqual(library, stripped);
                library = stripped;
            }

            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("Before").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldA").Offset);
            TranslatedNormalField fieldB = testStruct.FindDeclaration<TranslatedNormalField>("FieldB");
            Assert.Equal(8, fieldB.Offset);
            TranslatedNormalField fieldC = testStruct.FindDeclaration<TranslatedNormalField>("FieldC");
            Assert.Equal(12, fieldC.Offset);
            Assert.Equal(16, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);

            // Find the anonymous union type
            // We're ensuring there's only one to ensure that the reference from FieldB/FieldC did not keep the outer union alive.
            // (There should also only be one since FieldB and FieldC both share the same record.)
            TranslatedRecord? anonymousUnion = null;
            foreach (TranslatedRecord record in testStruct.OfType<TranslatedRecord>().Where(r => r.IsUnnamed))
            {
                Assert.Null(anonymousUnion);
                anonymousUnion = record;
            }
            Assert.NotNull(anonymousUnion);

            TranslatedTypeReference fieldBType = Assert.IsAssignableFrom<TranslatedTypeReference>(fieldB.Type);
            Assert.ReferenceEqual(anonymousUnion, fieldBType.TryResolve(library));

            TranslatedTypeReference fieldCType = Assert.IsAssignableFrom<TranslatedTypeReference>(fieldC.Type);
            Assert.ReferenceEqual(anonymousUnion, fieldCType.TryResolve(library));
        }

        [Fact]
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
            library = new LiftAnonymousRecordFieldsTransformation().Transform(library);

            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("Before").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldA").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldB").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldC").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("FieldD").Offset);
            Assert.Equal(8, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);

            AssertAnonymousTypesNotReferenced(library);
        }

        [Fact]
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
            library = new LiftAnonymousRecordFieldsTransformation().Transform(library);

            TranslatedRecord testStruct = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal(0, testStruct.FindDeclaration<TranslatedNormalField>("Before").Offset);
            Assert.Equal(4, testStruct.FindDeclaration<TranslatedNormalField>("Field").Offset);
            Assert.Equal(8, testStruct.FindDeclaration<TranslatedNormalField>("After").Offset);

            AssertAnonymousTypesNotReferenced(library);
        }

        [Fact]
        public void UnexpectedMemberCausesDiagnostic()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
void DummyFunction();
struct MyStruct
{
    struct
    {
        int x;
    };
};
"
            );

            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new UnexpectedMemberCausesDiagnosticTransformation().Transform(library);
            Assert.Empty(library.FindDeclaration<TranslatedRecord>("MyStruct").FindDeclaration<TranslatedNormalField>().Diagnostics); // Sanity
            library = new LiftAnonymousRecordFieldsTransformation().Transform(library);

            TranslatedRecord myStruct = library.FindDeclaration<TranslatedRecord>("MyStruct");
            Assert.Single(myStruct.Members.OfType<TranslatedNormalField>());
            TranslatedNormalField anonymousField = myStruct.FindDeclaration<TranslatedNormalField>();

            // Ensure that the transformation of the field was aborted and a diagnostic was added to indicate as such
            Assert.True(anonymousField.IsUnnamed);
            Assert.Single(anonymousField.Diagnostics);
            Assert.NotEqual((CSharpBuiltinTypeReference)CSharpBuiltinType.Int, anonymousField.Type);
        }

        private sealed class UnexpectedMemberCausesDiagnosticTransformation : TransformationBase
        {
            //TODO: We can't synthesize methods yet, so we steal a function and move it
            // https://github.com/InfectedLibraries/Biohazrd/issues/62
            private TranslatedFunction? Function;

            protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
            {
                if (Function is null)
                {
                    Function = declaration;
                    return null;
                }
                else
                { return declaration; }
            }

            protected override TransformationResult TransformRecord(TransformationContext context, TranslatedRecord declaration)
            {
                if (declaration.IsUnnamed)
                {
                    // Add a method to the anonymous record
                    // (Anonymous types cannot contain anything besides public non-static fields and other anonymous types, so this angers the transformation.)
                    Assert.NotNull(Function);
                    return declaration with
                    {
                        Members = declaration.Members.Add(Function)
                    };
                }
                else
                { return declaration; }
            }
        }
    }
}
