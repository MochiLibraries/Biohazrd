using Biohazrd.CSharp;
using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Common;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Biohazrd.Transformation.Tests
{
    public sealed class DeduplicateNamesTransformationTests : BiohazrdTestBase
    {
        [Fact]
        public void NotingToDo()
        {
            TranslatedLibrary library = CreateLibrary(@"void Test(int x, int y);");
            TranslatedLibrary transformed = new DeduplicateNamesTransformation().Transform(library);
            Assert.ReferenceEqual(library, transformed);
        }

        [Fact]
        public void BasicOperation1()
        {
            TranslatedLibrary library = CreateLibrary(@"void Test(int, int);");
            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("Test");
            Assert.Equal(function.Parameters[0].Name, function.Parameters[1].Name);

            library = new DeduplicateNamesTransformation().Transform(library);

            function = library.FindDeclaration<TranslatedFunction>("Test");
            Assert.NotEqual(function.Parameters[0].Name, function.Parameters[1].Name);
        }

        [Fact]
        public void BasicOperation2()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
namespace A
{
    class MyClass { };
}

namespace B
{
    class MyClass { };
}
"
            );

            // Quick and dirty transformation to remove namespaces, which causes the types to conflict.
            Assert.Equal(2, library.Declarations.Count);
            library = library with
            {
                Declarations = ImmutableList.Create
                (
                    library.Declarations[0] with { Namespace = null },
                    library.Declarations[1] with { Namespace = null }
                )
            };

            TranslatedLibrary transformed = new DeduplicateNamesTransformation().Transform(library);

            Assert.Equal(2, transformed.Declarations.Count);
            TranslatedRecord record0 = Assert.IsType<TranslatedRecord>(transformed.Declarations[0]);
            TranslatedRecord record1 = Assert.IsType<TranslatedRecord>(transformed.Declarations[1]);

            Assert.NotEqual(record0.Name, record1.Name);

            // Sanity check that the transformation changed things:
            Assert.NotReferenceEqual(library, transformed);
        }

        [Fact]
        public void OverloadsAreNotTransformed()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
void Test();
void Test(int);
"
            );

            foreach (TranslatedFunction function in library.OfType<TranslatedFunction>())
            { Assert.Equal("Test", function.Name); }

            TranslatedLibrary transformed = new DeduplicateNamesTransformation().Transform(library);

            foreach (TranslatedFunction function in transformed.OfType<TranslatedFunction>())
            { Assert.Equal("Test", function.Name); }

            // Sanity check that the transformation didn't change anything:
            Assert.ReferenceEqual(library, transformed);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/137")]
        public void NamespaceTest()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
namespace A
{
    class MyClass { };
}

namespace B
{
    class MyClass { };
}
"
            );

            TranslatedRecord aClass = library.FindDeclaration<TranslatedRecord>(d => d.Namespace == "A");
            TranslatedRecord bClass = library.FindDeclaration<TranslatedRecord>(d => d.Namespace == "B");
            Assert.Equal("MyClass", aClass.Name);
            Assert.Equal("MyClass", bClass.Name);
            Assert.NotEqual(aClass.Namespace, bClass.Namespace);

            TranslatedLibrary transformed = new DeduplicateNamesTransformation().Transform(library);

            TranslatedRecord aClassTransformed = transformed.FindDeclaration<TranslatedRecord>(d => d.Namespace == "A");
            TranslatedRecord bClassTransformed = transformed.FindDeclaration<TranslatedRecord>(d => d.Namespace == "B");

            Assert.Equal("MyClass", aClassTransformed.Name);
            Assert.Equal("MyClass", bClassTransformed.Name);

            // Sanity check that the transformation didn't change anything:
            Assert.ReferenceEqual(aClass, aClassTransformed);
            Assert.ReferenceEqual(bClass, bClassTransformed);
            Assert.ReferenceEqual(library, transformed);
        }

        [Fact]
        public void ClonedNonConflictingParentTest()
        {
            // Biohazrd allows the same declaration reference to be used in multiple contexts, this ensures that the transformation properly considers this
            // (This almost regressed while fixing https://github.com/InfectedLibraries/Biohazrd/issues/137)
            // If this situation isn't handled correctly, the transformation might try to "deduplicate" the `x` parameter on the two same-but-separate functions.
            TranslatedLibrary library = CreateLibrary("void Test(int x);");

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("Test");

            SynthesizedLooseDeclarationsTypeDeclaration containerA = new(TranslatedFile.Synthesized)
            {
                Name = "A",
                Members = ImmutableList.Create<TranslatedDeclaration>(function)
            };

            SynthesizedLooseDeclarationsTypeDeclaration containerB = new(TranslatedFile.Synthesized)
            {
                Name = "B",
                Members = ImmutableList.Create<TranslatedDeclaration>(function)
            };

            library = library with
            {
                Declarations = ImmutableList.Create<TranslatedDeclaration>(containerA, containerB)
            };

            // Sanity check that the functions have the same reference:
            Assert.ReferenceEqual
            (
                library.FindDeclaration<SynthesizedLooseDeclarationsTypeDeclaration>("A").FindDeclaration<TranslatedFunction>(),
                library.FindDeclaration<SynthesizedLooseDeclarationsTypeDeclaration>("B").FindDeclaration<TranslatedFunction>()
            );

            TranslatedLibrary transformed = new DeduplicateNamesTransformation().Transform(library);

            SynthesizedLooseDeclarationsTypeDeclaration transformedA = transformed.FindDeclaration<SynthesizedLooseDeclarationsTypeDeclaration>("A");
            TranslatedFunction transformedFunctionA = transformedA.FindDeclaration<TranslatedFunction>();
            Assert.Equal("A", transformedA.Name);
            Assert.Equal("Test", transformedFunctionA.Name);
            Assert.Equal("x", transformedFunctionA.Parameters[0].Name);

            SynthesizedLooseDeclarationsTypeDeclaration transformedB = transformed.FindDeclaration<SynthesizedLooseDeclarationsTypeDeclaration>("B");
            TranslatedFunction transformedFunctionB = transformedA.FindDeclaration<TranslatedFunction>();
            Assert.Equal("B", transformedB.Name);
            Assert.Equal("Test", transformedFunctionB.Name);
            Assert.Equal("x", transformedFunctionB.Parameters[0].Name);

            // Check that the transformation returned the same library that we passed in
            // This may seem unecessary, but the transformation's internal edge-case handling can cause a library to be modified even if no names are duplicate
            Assert.ReferenceEqual(library, transformed);
        }

        // This test has two variants because it fails differently depending on how a buggy transformation is implemented
        private void DuplicateParentWithDuplicateChildTest(bool shareParent)
        {
            TranslatedLibrary library = CreateLibrary("struct MyStruct { void Test(int, int); };");

            // Contrived example of having two records that need to be de-duplicated with functions that have parameters that need to be de-duplicated
            Assert.Single(library.Declarations);
            library = library with
            {
                Declarations = shareParent ? ImmutableList.Create(library.Declarations[0], library.Declarations[0])
                    : ImmutableList.Create(library.Declarations[0], library.Declarations[0] with { })
            };

            // Precondition sanity checks
            {
                Assert.Equal(2, library.Declarations.Count);

                if (shareParent)
                { Assert.ReferenceEqual(library.Declarations[0], library.Declarations[1]); }
                else
                { Assert.NotReferenceEqual(library.Declarations[0], library.Declarations[1]); }

                Assert.Equal(library.Declarations[0].Name, library.Declarations[1].Name);
                TranslatedFunction function0 = library.Declarations[0].FindDeclaration<TranslatedFunction>("Test");
                Assert.Equal(function0.Parameters[0].Name, function0.Parameters[1].Name);
                TranslatedFunction function1 = library.Declarations[1].FindDeclaration<TranslatedFunction>("Test");
                Assert.Equal(function1.Parameters[0].Name, function1.Parameters[1].Name);
            }

            // Transform and validate
            library = new DeduplicateNamesTransformation().Transform(library);
            {
                Assert.Equal(2, library.Declarations.Count);
                Assert.NotEqual(library.Declarations[0].Name, library.Declarations[1].Name);
                TranslatedFunction function0 = library.Declarations[0].FindDeclaration<TranslatedFunction>("Test");
                Assert.NotEqual(function0.Parameters[0].Name, function0.Parameters[1].Name);
                TranslatedFunction function1 = library.Declarations[1].FindDeclaration<TranslatedFunction>("Test");
                Assert.NotEqual(function1.Parameters[0].Name, function1.Parameters[1].Name);
            }
        }

        [Fact]
        public void DuplicateParentWithDuplicateChildTest0()
            => DuplicateParentWithDuplicateChildTest(false);

        [Fact]
        public void DuplicateParentWithDuplicateChildTest1()
            => DuplicateParentWithDuplicateChildTest(true);

        [Fact]
        public void NonFunctionConflictingWithFunctionIsDeduplicated()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct MyStruct
{
    int x;
    void Test();
};
"
            );

            // Rename the `x` field to `Test`
            {
                TranslatedRecord myStruct = library.FindDeclaration<TranslatedRecord>("MyStruct");
                TranslatedNormalField field = myStruct.FindDeclaration<TranslatedNormalField>("x");
                myStruct = myStruct with
                {
                    Members = myStruct.Members.Replace(field, field with { Name = "Test" })
                };

                Assert.Single(library.Declarations);
                library = library with { Declarations = ImmutableList.Create<TranslatedDeclaration>(myStruct) };

                // Sanity
                myStruct = library.FindDeclaration<TranslatedRecord>("MyStruct");
                Assert.Equal("Test", myStruct.FindDeclaration<TranslatedFunction>().Name);
                Assert.Equal("Test", myStruct.FindDeclaration<TranslatedNormalField>().Name);
            }

            // Transform and validate
            library = new DeduplicateNamesTransformation().Transform(library);
            {
                TranslatedRecord myStruct = library.FindDeclaration<TranslatedRecord>("MyStruct");
                Assert.Equal("Test", myStruct.FindDeclaration<TranslatedFunction>().Name);
                Assert.NotEqual("Test", myStruct.FindDeclaration<TranslatedNormalField>().Name);
            }
        }
    }
}
