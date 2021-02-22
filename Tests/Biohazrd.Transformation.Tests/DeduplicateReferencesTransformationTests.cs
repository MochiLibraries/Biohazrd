using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Infrastructure;
using System.Collections.Generic;
using Xunit;

namespace Biohazrd.Transformation.Tests
{
    public sealed class DeduplicateReferencesTransformationTests : BiohazrdTestBase
    {
        [Fact]
        public void DoesNothingWhenUnecessary()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
void Function(int x, int y);
struct MyStruct
{
    int field;
};
"
            );

            TranslatedLibrary transformed = new DeduplicateReferencesTransformation().Transform(library);
            Assert.ReferenceEqual(library, transformed);
        }

        [Fact]
        public void BasicOperation()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
void Function(int x, int y);
struct MyStruct
{
    int field;
};
"
            );

            // Count the number of declarations in the base library (Used for validation later)
            int declarationCount = 0;
            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            { declarationCount++; }
            Assert.True(declarationCount > 0);
            Assert.Equal(5, declarationCount); // Sanity

            library = library with
            {
                Declarations = library.Declarations.AddRange(library.Declarations)
            };

            // Sanity check
            HashSet<TranslatedDeclaration> uniqueReferencesInOriginal = new(ReferenceEqualityComparer.Instance);
            {
                int duplicateCount = 0;
                foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
                {
                    if (!uniqueReferencesInOriginal.Add(declaration))
                    { duplicateCount++; }
                }

                Assert.Equal(declarationCount, uniqueReferencesInOriginal.Count);
                Assert.Equal(declarationCount, duplicateCount);
            }

            // Transform and validate
            library = new DeduplicateReferencesTransformation().Transform(library);
            {
                HashSet<TranslatedDeclaration> uniqueReferencesInTransformed = new(ReferenceEqualityComparer.Instance);
                foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
                {
                    // Remove this declaration from the list of original references
                    // (This is to ensure the transformation didn't just clone the entire library, they should all still be present just not more than once.)
                    uniqueReferencesInOriginal.Remove(declaration);

                    // Ensure each declaration within the transformed library is a unique reference
                    Assert.True(uniqueReferencesInTransformed.Add(declaration));
                }

                Assert.Equal(declarationCount * 2, uniqueReferencesInTransformed.Count); // There should be two references in the transformed library for every one in the original
                Assert.Empty(uniqueReferencesInOriginal); // If this set still contains elements, the transformation was overly zealous and cloned things it didn't have to
            }
        }
    }
}
