using Biohazrd.Expressions;
using Biohazrd.Tests.Common;
using Biohazrd.Transformation;
using Biohazrd.Transformation.Common;
using System.Collections.Immutable;
using System.Linq;
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

        [Fact]
        public void Constant_DoNotAllowInGlobalScope()
        {
            TranslatedLibrary library = CreateLibrary("struct MyStruct { };");
            library = new SimpleTransformation()
            {
                TransformRecord = (context, declaration) =>
                {
                    TranslatedConstant nestedConstant = new("NestedConstant", new StringConstant("I'm valid in both C++ and C#!"));
                    TransformationResult result = declaration with { Members = declaration.Members.Add(nestedConstant) };
                    result.Add(new TranslatedConstant("GlobalConstant", new StringConstant("I'm valid in C++ but not in C#!")));
                    return result;
                }
            }.Transform(library);
            library = new CSharpTranslationVerifier().Transform(library);

            TranslatedConstant nestedConstant = library.FindDeclaration<TranslatedRecord>("MyStruct").FindDeclaration<TranslatedConstant>("NestedConstant");
            TranslatedConstant globalConstant = library.FindDeclaration<TranslatedConstant>("GlobalConstant");
            Assert.Empty(nestedConstant.Diagnostics);
            Assert.NotEmpty(globalConstant.Diagnostics.Where(d => d.IsError));
        }

        [Fact]
        public void Constant_InferredCSharpConstantTypeOk()
        {
            TranslatedLibrary library = CreateLibrary("struct MyStruct { };");
            library = new SimpleTransformation()
            {
                TransformRecord = (context, declaration) => declaration with
                {
                    Members = declaration.Members.Add(new TranslatedConstant("Constant", IntegerConstant.FromInt32(3226)))
                }
            }.Transform(library);
            library = new CSharpTranslationVerifier().Transform(library);

            TranslatedConstant constant = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedConstant>();
            Assert.Empty(constant.Diagnostics);
        }

        [Fact]
        public void Constant_ExplicitCSharpConstantTypeOk()
        {
            TranslatedLibrary library = CreateLibrary("struct MyStruct { };");
            library = new SimpleTransformation()
            {
                TransformRecord = (context, declaration) => declaration with
                {
                    Members = declaration.Members.Add
                    (
                        new TranslatedConstant("Constant", IntegerConstant.FromInt32(3226)) { Type = CSharpBuiltinType.Short }
                    )
                }
            }.Transform(library);
            library = new CSharpTranslationVerifier().Transform(library);

            TranslatedConstant constant = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedConstant>();
            Assert.Empty(constant.Diagnostics);
        }

        [Fact]
        public void Constant_EnumTypeOk()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
enum MyEnum { A, B, C };
struct MyStruct { };
"
            );
            library = new SimpleTransformation()
            {
                TransformRecord = (context, declaration) => declaration with
                {
                    Members = declaration.Members.Add
                    (
                        new TranslatedConstant("Constant", IntegerConstant.FromInt32(3226)) { Type = TranslatedTypeReference.Create(library.FindDeclaration<TranslatedEnum>()) }
                    )
                }
            }.Transform(library);
            library = new CSharpTranslationVerifier().Transform(library);

            TranslatedConstant constant = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedConstant>();
            Assert.Empty(constant.Diagnostics);
        }

        [Fact]
        public void Constant_StructTypeNotOk()
        {
            TranslatedLibrary library = CreateLibrary("struct MyStruct { };");
            library = new SimpleTransformation()
            {
                TransformRecord = (context, declaration) => declaration with
                {
                    Members = declaration.Members.Add
                    (
                        new TranslatedConstant("Constant", IntegerConstant.FromInt32(3226)) { Type = TranslatedTypeReference.Create(declaration) }
                    )
                }
            }.Transform(library);
            library = new CSharpTranslationVerifier().Transform(library);

            TranslatedConstant constant = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedConstant>();
            Assert.NotEmpty(constant.Diagnostics.Where(d => d.IsError));
        }

        [Fact]
        public void Constant_UnsupportedConstantNotOk()
        {
            TranslatedLibrary library = CreateLibrary("struct MyStruct { };");
            library = new SimpleTransformation()
            {
                TransformRecord = (context, declaration) => declaration with
                {
                    Members = declaration.Members.Add
                    (
                        new TranslatedConstant("Constant", new UnsupportedConstantExpression(nameof(Constant_UnsupportedConstantNotOk)))
                    )
                }
            }.Transform(library);
            library = new CSharpTranslationVerifier().Transform(library);

            TranslatedConstant constant = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedConstant>();
            Assert.NotEmpty(constant.Diagnostics.Where(d => d.IsError));

            // Unsupported constants should mention the failure reason
            Assert.Single(constant.Diagnostics.Where(d => d.Message.Contains(nameof(Constant_UnsupportedConstantNotOk))));
        }

        [Fact]
        [RelatedIssue("https://github.com/MochiLibraries/Biohazrd/issues/31")]
        public void Record_CannotInitializeConstructorlessVirtual()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class MyClass
{
public:
    virtual void Test() { }
};
"
            );

            Assert.Empty(library.FindDeclaration<TranslatedRecord>("MyClass").Diagnostics);
            library = new CSharpTranslationVerifier().Transform(library);
            Assert.Contains(library.FindDeclaration<TranslatedRecord>("MyClass").Diagnostics, d => d.Severity >= Severity.Warning);
        }

        [Fact]
        [RelatedIssue("https://github.com/MochiLibraries/Biohazrd/issues/31")]
        public void Record_CannotInitializeConstructorlessVirtual_IgnoreAbstract()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class MyClass
{
public:
    virtual void Test() = 0;
};
"
            );

            Assert.Empty(library.FindDeclaration<TranslatedRecord>("MyClass").Diagnostics);
            library = new CSharpTranslationVerifier().Transform(library);
            Assert.Empty(library.FindDeclaration<TranslatedRecord>("MyClass").Diagnostics);
        }

        [Fact]
        [RelatedIssue("https://github.com/MochiLibraries/Biohazrd/issues/31")]
        public void Record_CannotInitializeConstructorlessVirtual_ExplicitConstructorOk()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class MyClass
{
public:
    MyClass() { }
    virtual void Test() { }
};
"
            );

            Assert.Empty(library.FindDeclaration<TranslatedRecord>("MyClass").Diagnostics);
            library = new CSharpTranslationVerifier().Transform(library);
            Assert.Empty(library.FindDeclaration<TranslatedRecord>("MyClass").Diagnostics);
        }
    }
}
