using Biohazrd.Tests.Common;
using System;
using Xunit;

namespace Biohazrd.CSharp.Tests
{
    // Usage of CSharpBuiltinTypeTransformation in this file is mainly being done because we plan to eliminate it.
    // https://github.com/MochiLibraries/Biohazrd/issues/107
    public sealed class CSharpTypeReductionTransformationTests : BiohazrdTestBase
    {
        private void NativeIntegerTest(string? include, string typeName, TypeReference expectedType)
        {
            string code = $"{typeName} Test();";

            if (include is not null)
            { code = $"#include <{include}>{Environment.NewLine}{code}"; }

            // size_t without including anything is a non-standard MSVC feature so it only works on Windows triples
            TranslatedLibrary library = CreateLibrary(code, targetTriple: include is null ? "x86_64-pc-windows" : null);
            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new CSharpBuiltinTypeTransformation().Transform(library);
            library = new ResolveTypedefsTransformation().Transform(library);
            TypeReference returnType = library.FindDeclaration<TranslatedFunction>("Test").ReturnType;
            Assert.Equal(expectedType, returnType);
        }

        [Fact]
        [RelatedIssue("https://github.com/MochiLibraries/Biohazrd/issues/82")]
        public void size_t_Intrinsic()
            => NativeIntegerTest(null, "size_t", CSharpBuiltinType.UnsignedNativeInt);

        [Fact]
        [RelatedIssue("https://github.com/MochiLibraries/Biohazrd/issues/82")]
        public void size_t_StdDef()
            => NativeIntegerTest("stddef.h", "size_t", CSharpBuiltinType.UnsignedNativeInt);

        [Fact]
        [RelatedIssue("https://github.com/MochiLibraries/Biohazrd/issues/82")]
        public void size_t_Namespaced()
            => NativeIntegerTest("cstddef", "std::size_t", CSharpBuiltinType.UnsignedNativeInt);

        [Fact]
        [RelatedIssue("https://github.com/MochiLibraries/Biohazrd/issues/82")]
        public void ptrdiff_t()
            => NativeIntegerTest("stddef.h", "ptrdiff_t", CSharpBuiltinType.NativeInt);

        [Fact]
        [RelatedIssue("https://github.com/MochiLibraries/Biohazrd/issues/82")]
        public void ptrdiff_t_Namespaced()
            => NativeIntegerTest("cstddef", "std::ptrdiff_t", CSharpBuiltinType.NativeInt);

        [Fact]
        [RelatedIssue("https://github.com/MochiLibraries/Biohazrd/issues/82")]
        public void inptr_t()
            => NativeIntegerTest("cstdint", "intptr_t", CSharpBuiltinType.NativeInt);

        [Fact]
        [RelatedIssue("https://github.com/MochiLibraries/Biohazrd/issues/82")]
        public void uinptr_t()
            => NativeIntegerTest("cstdint", "uintptr_t", CSharpBuiltinType.UnsignedNativeInt);

        [Fact]
        [RelatedIssue("https://github.com/MochiLibraries/Biohazrd/issues/82")]
        public void UseNativeIntegersForPointerSizedTypes_False()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#include <cstddef>
#include <cstdint>
void Test(size_t, ptrdiff_t, intptr_t, uintptr_t);
"
            );

            library = new CSharpTypeReductionTransformation() { UseNativeIntegersForPointerSizedTypes = false }.Transform(library);
            library = new CSharpBuiltinTypeTransformation().Transform(library);
            library = new ResolveTypedefsTransformation().Transform(library);

            foreach (TranslatedParameter parameter in library.FindDeclaration<TranslatedFunction>("Test").Parameters)
            {
                Assert.NotEqual(CSharpBuiltinType.NativeInt, parameter.Type);
                Assert.NotEqual(CSharpBuiltinType.UnsignedNativeInt, parameter.Type);
            }
        }
    }
}
