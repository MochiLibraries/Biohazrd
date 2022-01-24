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
            library = new CSharpTypeReductionTransformation().Transform(library);
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
            library = new CSharpTypeReductionTransformation().Transform(library);
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
            library = new CSharpTypeReductionTransformation().Transform(library);
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
            library = new CSharpTypeReductionTransformation().Transform(library);
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
            library = new CSharpTypeReductionTransformation().Transform(library);
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
            library = new CSharpTypeReductionTransformation().Transform(library);
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

        [Fact]
        public void DefaultParameterValue_UserStructNotAllowed()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#include <stddef.h>
struct TestStruct
{
    void Test(TestStruct x = TestStruct());
};
"
            );

            // Validate preconditition: ClangSharp.Pathogen will fail to interpret the default value and it will be marked as such
            TranslatedParameter oldParameter = library.FindDeclaration("TestStruct").FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("x");
            TranslationDiagnostic oldDiagnostic = Assert.Single(oldParameter.Diagnostics, d => d.Severity == Severity.Warning && d.Message.StartsWith("Unsupported constant:"));

            // Apply transformations
            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new CSharpTranslationVerifier().Transform(library);

            // Validate postcondition: The default value should be removed and no extra diagnostics should be added.
            TranslatedParameter parameter = library.FindDeclaration("TestStruct").FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("x");
            Assert.Null(parameter.DefaultValue);
            TranslationDiagnostic newDiagnostic = Assert.Single(parameter.Diagnostics);
            Assert.Equal(oldDiagnostic.ToString(), newDiagnostic.ToString());
        }

        [Fact]
        public void DefaultParameterValue_UnsupportedConstantExpression()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#include <stddef.h>
struct TestStruct
{
    void Test(TestStruct x);
};
"
            );

            // Validate preconditition: Parameter should not have any diagnostics
            Assert.Empty(library.FindDeclaration("TestStruct").FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("x").Diagnostics);

            // Manually set the default value to a UnsupportedConstantExpression
            // (Normally when these constants happen naturally they're already removed and converted into diagnostics by the time translation has completed.)
            const string message = "Constant value machine broke";
            library = new SimpleTransformation()
            {
                TransformParameter = (c, p) => p with { DefaultValue = new UnsupportedConstantExpression(message) }
            }.Transform(library);

            // Apply transformations
            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new CSharpTranslationVerifier().Transform(library);

            // Validate postcondition: The default value should be removed and a diagnostic should have been added.
            TranslatedParameter parameter = library.FindDeclaration("TestStruct").FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("x");
            Assert.Null(parameter.DefaultValue);
            TranslationDiagnostic newDiagnostic = Assert.Single(parameter.Diagnostics);
            Assert.Contains(message, newDiagnostic.Message);
            Assert.Equal(Severity.Warning, newDiagnostic.Severity);
        }

        [Fact]
        public void DefaultParameterValue_UnrecognizedNotAllowed()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#include <stddef.h>
struct TestStruct
{
    void Test(int x = 100);
};
"
            );
            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new SimpleTransformation()
            {
                // Transform the parameter to be a type which isn't recognized by C# as defaultable
                TransformParameter = (c, p) => p with { Type = new ExternallyDefinedTypeReference("DummyType") }
            }.Transform(library);
            Assert.Empty(library.FindDeclaration("TestStruct").FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("x").Diagnostics);
            library = new CSharpTranslationVerifier().Transform(library);
            TranslatedParameter parameter = library.FindDeclaration("TestStruct").FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("x");
            Assert.Null(parameter.DefaultValue); // The unusable default should be removed.
            TranslationDiagnostic diagnostic = Assert.Single(parameter.Diagnostics, d => d.Message == "Default parameter values are not supported for this parameter's type.");
            Assert.Equal(Severity.Warning, diagnostic.Severity);
        }

        [Fact]
        public void DefaultParameterValue_NativeIntegerAllowed()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#include <stddef.h>
struct TestStruct
{
    void Test(size_t x = 100);
};
"
            );
            Assert.Empty(library.FindDeclaration("TestStruct").FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("x").Diagnostics);
            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new ResolveTypedefsTransformation().Transform(library);
            library = new CSharpTranslationVerifier().Transform(library);
            TranslatedParameter parameter = library.FindDeclaration("TestStruct").FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("x");
            Assert.Empty(parameter.Diagnostics);
        }

        [Fact]
        public void DefaultParameterValue_NonStandardNativeIntegerAllowed()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#include <stddef.h>
struct TestStruct
{
    void Test(size_t x = 100);
};
"
            );
            Assert.Empty(library.FindDeclaration("TestStruct").FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("x").Diagnostics);
            library = new SimpleTransformation()
            {
                TransformParameter = (context, parameter) => parameter with { Type = new ExternallyDefinedTypeReference("nuint") }
            }.Transform(library);
            library = new CSharpTranslationVerifier().Transform(library);
            TranslatedParameter parameter = library.FindDeclaration("TestStruct").FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("x");
            Assert.Empty(parameter.Diagnostics);
        }
    }
}
