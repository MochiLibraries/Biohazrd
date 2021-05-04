using Biohazrd.Metadata;
using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Common;
using System.Linq;
using Xunit;

namespace Biohazrd.Transformation.Tests
{
    [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/188")]
    public sealed class StripUnreferencedLazyDeclarationsTransformationTests : BiohazrdTestBase
    {
        private sealed class MarkLazyRecordsForTestTransformation : TransformationBase
        {
            protected override TransformationResult TransformRecord(TransformationContext context, TranslatedRecord declaration)
                => declaration.Name.StartsWith('_') ? declaration with { Metadata = declaration.Metadata.Add<LazilyGenerated>() } : declaration;
        }

        private TranslatedLibrary CreateLazyLibrary(string cppCode)
        {
            TranslatedLibrary library = CreateLibrary(cppCode);
            library = new MarkLazyRecordsForTestTransformation().Transform(library);
            library = new TypeReductionTransformation().Transform(library);
            return library;
        }

        [Fact]
        public void TestMarkLazyRecordsForTestTransformationForSanity()
        {
            TranslatedLibrary library = CreateLibrary("struct _Test { int x; }; struct Test { int x; };");
            TranslatedRecord normal = library.FindDeclaration<TranslatedRecord>("Test");
            TranslatedRecord lazy = library.FindDeclaration<TranslatedRecord>("_Test");

            TranslatedLibrary transformed = new MarkLazyRecordsForTestTransformation().Transform(library);
            TranslatedRecord transformedNormal = transformed.FindDeclaration<TranslatedRecord>("Test");
            TranslatedRecord transformedLazy = transformed.FindDeclaration<TranslatedRecord>("_Test");

            Assert.ReferenceEqual(normal, transformedNormal);
            Assert.ReferenceEqual(lazy.Original, transformedLazy.Original);
            Assert.False(normal.Metadata.Has<LazilyGenerated>());
            Assert.False(lazy.Metadata.Has<LazilyGenerated>());
            Assert.False(transformedNormal.Metadata.Has<LazilyGenerated>());
            Assert.True(transformedLazy.Metadata.Has<LazilyGenerated>());
        }

        [Fact]
        public void NoChangesWithNoLazy()
        {
            TranslatedLibrary library = CreateLazyLibrary("struct A { int x; }; void Test();");
            TranslatedLibrary transformed = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);
            Assert.ReferenceEqual(library, transformed);
        }

        [Fact]
        public void NoChangesWithAllReferenced()
        {
            TranslatedLibrary library = CreateLazyLibrary("struct _A { int x; }; _A Test();");
            TranslatedLibrary transformed = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);
            Assert.ReferenceEqual(library, transformed);
        }

        [Fact]
        public void UnreferencedIsRemoved()
        {
            TranslatedLibrary library = CreateLazyLibrary("struct _A { int x; }; void Test();");
            TranslatedLibrary transformed = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);
            Assert.NotReferenceEqual(library, transformed);
            Assert.Empty(transformed.Declarations.OfType<TranslatedRecord>());
        }

        [Fact]
        public void UnreferencedParentOfReferencedIsNotRemoved()
        {
            TranslatedLibrary library = CreateLazyLibrary
            (@"
struct _A
{
    int x;
    struct _B
    {
        int y;
    };
};

_A::_B Test();
"
            );
            TranslatedLibrary transformed = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);
            Assert.ReferenceEqual(library, transformed);
        }

        [Fact]
        public void UnreferencedParentOfReferencedNonLazyIsNotRemoved()
        {
            TranslatedLibrary library = CreateLazyLibrary
            (@"
struct _A
{
    int x;
    struct B
    {
        int y;
    };
};

_A::B Test();
"
            );
            TranslatedLibrary transformed = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);
            Assert.ReferenceEqual(library, transformed);
        }

        [Fact]
        public void UnreferencedChildFromReferencedParentIsRemoved()
        {
            TranslatedLibrary library = CreateLazyLibrary
            (@"
struct _A
{
    int x;
    struct _B
    {
        int y;
    };
};

_A Test();
"
);
            library = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);

            TranslatedRecord parentRecord = library.FindDeclaration<TranslatedRecord>("_A");
            Assert.Empty(parentRecord.Members.OfType<TranslatedRecord>());
        }

        [Fact]
        public void LazyRecordReferencedByReferencedLazyRecordIsNotRemoved()
        {
            TranslatedLibrary library = CreateLazyLibrary
            (@"
struct _A
{
    int x;
};

struct _B
{
    _A x;
};

_B Test();
"
            );
            TranslatedLibrary transformed = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);
            Assert.ReferenceEqual(library, transformed);
        }

        [Fact]
        public void LazyRecordReferencedByUnreferencedLazyRecordIsRemoved()
        {
            TranslatedLibrary library = CreateLazyLibrary
            (@"
struct _A
{
    int x;
};

struct _B
{
    _A x;
};

void Test();
"
            );
            TranslatedLibrary transformed = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);
            Assert.NotReferenceEqual(library, transformed);
            Assert.Empty(transformed.Declarations.OfType<TranslatedRecord>());
        }

        [Fact]
        public void MuautalLazyRecordReferences()
        {
            TranslatedLibrary library = CreateLazyLibrary
            (@"
struct _B;

struct _A
{
    int x;
    _B GetB();
};

struct _B
{
    _A x;
};

_A Test();
"
);
            TranslatedLibrary transformed = new StripUnreferencedLazyDeclarationsTransformation().Transform(library);
            Assert.ReferenceEqual(library, transformed);
        }
    }
}
