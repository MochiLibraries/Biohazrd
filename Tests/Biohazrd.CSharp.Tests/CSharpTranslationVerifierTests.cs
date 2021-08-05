using Biohazrd.Tests.Common;
using Biohazrd.Transformation;
using System.Collections.Immutable;
using Xunit;

namespace Biohazrd.CSharp.Tests
{
    public sealed class CSharpTranslationVerifierTests : BiohazrdTestBase
    {
        private class StripDeclarationDiagnosticsTransformation : TransformationBase
        {
            protected override TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
                => declaration with { Diagnostics = ImmutableArray<TranslationDiagnostic>.Empty };
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/120")]
        public void Enum_DoNotAllowLooseInGlobalScope()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
enum
{
    A,
    B,
    C
};
"
            );

            // Assert preconditions
            {
                TranslatedEnum translatedEnum = library.FindDeclaration<TranslatedEnum>();
                Assert.True(translatedEnum.TranslateAsLooseConstants);
                Assert.True(translatedEnum.IsUnnamed);
                Assert.Empty(translatedEnum.Diagnostics);
            }

            // Transform and validate
            library = new CSharpBuiltinTypeTransformation().Transform(library);
            library = new CSharpTranslationVerifier().Transform(library);
            {
                TranslatedEnum translatedEnum = library.FindDeclaration<TranslatedEnum>();
                Assert.False(translatedEnum.TranslateAsLooseConstants);
                Assert.True(translatedEnum.IsUnnamed);
                Assert.NotEmpty(translatedEnum.Diagnostics);
            }
        }

        [Fact]
        public void Enum_AllowLooseInRecordScope()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct MyStruct
{
    enum
    {
        A,
        B,
        C
    };
};
"
            );

            // Assert preconditions
            {
                TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("MyStruct");
                TranslatedEnum translatedEnum = record.FindDeclaration<TranslatedEnum>();
                Assert.True(translatedEnum.TranslateAsLooseConstants);
                Assert.True(translatedEnum.IsUnnamed);
                Assert.Empty(translatedEnum.Diagnostics);
            }

            // Transform and validate
            library = new CSharpBuiltinTypeTransformation().Transform(library);
            TranslatedLibrary transformed = new CSharpTranslationVerifier().Transform(library);
            Assert.ReferenceEqual(library, transformed);
        }

        [Fact]
        public void Enum_RequireLooseForInvalidBaseType()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct MyStruct
{
    enum class MyEnum : wchar_t
    {
        A,
        B,
        C
    };
};
",
                // 32-bit wchar_t is not currently supported so we need to use Windows
                // https://github.com/InfectedLibraries/Biohazrd/issues/45
                targetTriple: "x86_64-pc-win32"
            );

            // Assert preconditions
            library = new CSharpBuiltinTypeTransformation().Transform(library);
            {
                TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("MyStruct");
                TranslatedEnum translatedEnum = record.FindDeclaration<TranslatedEnum>("MyEnum");
                Assert.False(translatedEnum.TranslateAsLooseConstants);
                CSharpBuiltinTypeReference underlyingCSharpType = Assert.IsType<CSharpBuiltinTypeReference>(translatedEnum.UnderlyingType);
                Assert.Equal(CSharpBuiltinType.Char, underlyingCSharpType);
                Assert.Empty(translatedEnum.Diagnostics);
            }

            // Transform and validate
            library = new CSharpTranslationVerifier().Transform(library);
            {
                TranslatedRecord record = library.FindDeclaration<TranslatedRecord>("MyStruct");
                TranslatedEnum translatedEnum = record.FindDeclaration<TranslatedEnum>("MyEnum");
                Assert.True(translatedEnum.TranslateAsLooseConstants);
                Assert.NotEmpty(translatedEnum.Diagnostics);
            }
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/120")]
        public void Enum_CreateContainerForInvalidBaseTypeInGlobalScope1()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
enum : wchar_t
{
    A,
    B,
    C
};
",
                // 32-bit wchar_t is not currently supported so we need to use Windows
                // https://github.com/InfectedLibraries/Biohazrd/issues/45
                targetTriple: "x86_64-pc-win32"
            );

            // Assert preconditions
            library = new CSharpBuiltinTypeTransformation().Transform(library);
            {
                TranslatedEnum translatedEnum = library.FindDeclaration<TranslatedEnum>();
                Assert.True(translatedEnum.IsUnnamed);
                Assert.True(translatedEnum.TranslateAsLooseConstants);
                CSharpBuiltinTypeReference underlyingCSharpType = Assert.IsType<CSharpBuiltinTypeReference>(translatedEnum.UnderlyingType);
                Assert.Equal(CSharpBuiltinType.Char, underlyingCSharpType);
                Assert.Empty(translatedEnum.Diagnostics);
            }

            // Transform and validate
            library = new CSharpTranslationVerifier().Transform(library);
            {
                SynthesizedLooseDeclarationsTypeDeclaration looseContainer = library.FindDeclaration<SynthesizedLooseDeclarationsTypeDeclaration>();
                TranslatedEnum translatedEnum = looseContainer.FindDeclaration<TranslatedEnum>();
                Assert.True(translatedEnum.TranslateAsLooseConstants);
                Assert.NotEmpty(translatedEnum.Diagnostics);
            }
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/120")]
        public void Enum_CreateContainerForInvalidBaseTypeInGlobalScope2()
        {
            // This variant of this test ensure that the container takes on various attributes of the loose enum
            // (We're assuming a transformation fo some kind affected it.)
            TranslatedLibrary library = CreateLibrary
            (@"
enum : wchar_t
{
    A,
    B,
    C
};
",
                // 32-bit wchar_t is not currently supported so we need to use Windows
                // https://github.com/InfectedLibraries/Biohazrd/issues/45
                targetTriple: "x86_64-pc-win32"
            );

            // Fill in some missing information on the enum
            {
                TranslatedEnum translatedEnum = library.FindDeclaration<TranslatedEnum>();
                Assert.Single(library.Declarations);
                library = library with
                {
                    Declarations = ImmutableList.Create<TranslatedDeclaration>
                    (
                        translatedEnum with
                        {
                            Name = "MyEnum",
                            Namespace = "MyNamespace",
                            Accessibility = AccessModifier.Internal
                        }
                    )
                };
            }

            // Assert preconditions
            library = new CSharpBuiltinTypeTransformation().Transform(library);
            {
                TranslatedEnum translatedEnum = library.FindDeclaration<TranslatedEnum>();
                Assert.Equal("MyEnum", translatedEnum.Name);
                Assert.Equal("MyNamespace", translatedEnum.Namespace);
                Assert.Equal(AccessModifier.Internal, translatedEnum.Accessibility);
                Assert.True(translatedEnum.TranslateAsLooseConstants);
                CSharpBuiltinTypeReference underlyingCSharpType = Assert.IsType<CSharpBuiltinTypeReference>(translatedEnum.UnderlyingType);
                Assert.Equal(CSharpBuiltinType.Char, underlyingCSharpType);
                Assert.Empty(translatedEnum.Diagnostics);
            }

            // Transform and validate
            library = new CSharpTranslationVerifier().Transform(library);
            {
                SynthesizedLooseDeclarationsTypeDeclaration looseContainer = library.FindDeclaration<SynthesizedLooseDeclarationsTypeDeclaration>();
                TranslatedEnum translatedEnum = looseContainer.FindDeclaration<TranslatedEnum>();
                Assert.Equal("MyEnum", looseContainer.Name);
                Assert.Equal("MyNamespace", looseContainer.Namespace);
                Assert.Equal(AccessModifier.Internal, looseContainer.Accessibility);
                Assert.True(translatedEnum.TranslateAsLooseConstants);
                Assert.NotEmpty(translatedEnum.Diagnostics);
            }
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/134")]
        public void Function_UncallableFunctionsAreHandled()
        {
            // Uncallable functions are handled naturally since they will usually have errors attached, this test sanity-checks that the verifier handles them properly and that
            // they have an error added if none is present.
            TranslatedLibrary library = CreateLibrary
            (@"
struct MyStruct;
MyStruct Test();
"
            );

            // Strip off the errors added by the translation stage
            library = new StripDeclarationDiagnosticsTransformation().Transform(library);

            // Assert preconditions
            {
                TranslatedFunction translatedFunction = library.FindDeclaration<TranslatedFunction>();
                Assert.False(translatedFunction.IsCallable);
                Assert.DoesNotContain(translatedFunction.Diagnostics, d => d.IsError);
            }

            // Transform and validate
            library = new CSharpBuiltinTypeTransformation().Transform(library);
            library = new CSharpTranslationVerifier().Transform(library);
            {
                TranslatedFunction translatedFunction = library.FindDeclaration<TranslatedFunction>();
                Assert.False(translatedFunction.IsCallable);
                Assert.Contains(translatedFunction.Diagnostics, d => d.IsError);
            }
        }
    }
}
