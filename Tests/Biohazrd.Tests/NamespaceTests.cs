using Biohazrd.Tests.Common;
using System;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class NamespaceTests : BiohazrdTestBase
    {
        [Fact]
        public void NoNamespaceIsNull()
        {
            TranslatedLibrary library = CreateLibrary(@"struct Test { };");
            TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Null(record.Namespace);
        }

        [Fact]
        public void NamespaceCannotBeSetToEmptyString()
        {
            TranslatedLibrary library = CreateLibrary(@"struct Test { };");
            TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Throws<InvalidOperationException>(() => record with { Namespace = "" });
        }

        [Fact]
        public void BasicNamespace()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
namespace MyNamespace
{
    struct Test { };
}
"
            );
            TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal("MyNamespace", record.Namespace);
        }

        [Fact]
        public void BasicMultiLevelNamespace0()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
namespace OuterNamespace
{
    namespace MyNamespace
    {
        struct Test { };
    }
}
"
            );
            TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal("OuterNamespace.MyNamespace", record.Namespace);
        }

        [Fact]
        public void BasicMultiLevelNamespace1()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
namespace OuterNamespace::MyNamespace
{
    struct Test { };
}
"
            );
            TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal("OuterNamespace.MyNamespace", record.Namespace);
        }

        [Fact]
        public void InsideExternC0()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
namespace OuterNamespace
{
    namespace MyNamespace
    {
        extern ""C""
        {
            struct Test { };
        }
    }
}
"
            );
            TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal("OuterNamespace.MyNamespace", record.Namespace);
        }

        [Fact]
        public void InsideExternC1()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
namespace OuterNamespace
{
    extern ""C""
    {
        namespace MyNamespace
        {
            struct Test { };
        }
    }
}
"
            );
            TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal("OuterNamespace.MyNamespace", record.Namespace);
        }

        [Fact]
        public void InsideExternC2()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
extern ""C""
{
    namespace OuterNamespace
    {
        namespace MyNamespace
        {
            struct Test { };
        }
    }
}
"
            );
            TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal("OuterNamespace.MyNamespace", record.Namespace);
        }

        [Fact]
        public void NestedTypeHasNamespaceOfParentType()
        {
            // The justification for this behavior is it lets the namespace make sense if a transformation un-nests the declaration.
            TranslatedLibrary library = CreateLibrary
            (@"
namespace MyNamespace
{
    struct Test
    {
        struct NestedType { };
    };
}
"
            );

            TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("Test");
            Assert.Equal("MyNamespace", record.Namespace);

            TranslatedRecord nestedRecord = record.FindDeclaration<TranslatedRecord>("NestedType");
            Assert.Equal("MyNamespace", nestedRecord.Namespace);
        }
    }
}
