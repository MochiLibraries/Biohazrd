using Biohazrd.CSharp;
using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Common;
using ClangSharp.Interop;
using Xunit;

namespace Biohazrd.Transformation.Tests
{
    public sealed class TypeReductionTransformationTests : BiohazrdTestBase
    {
        private void AssertVoidIntFunctionPointerType(TypeReference type)
        {
            Assert.IsType<FunctionPointerTypeReference>(type);
            FunctionPointerTypeReference functionPointer = (FunctionPointerTypeReference)type;

            Assert.IsType<VoidTypeReference>(functionPointer.ReturnType);

            Assert.Single(functionPointer.ParameterTypes);
            Assert.IsType<ClangTypeReference>(functionPointer.ParameterTypes[0]);
            ClangTypeReference parameterType = (ClangTypeReference)functionPointer.ParameterTypes[0];
            Assert.Equal(CXTypeKind.CXType_Int, parameterType.ClangType.Kind);
        }

        [Fact]
        public void FunctionPointerTest1()
        {
            TranslatedLibrary library = CreateLibrary(@"void Test(void (*function)(int));");

            library = new TypeReductionTransformation().Transform(library);

            TranslatedParameter parameter = library.FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("function");
            AssertVoidIntFunctionPointerType(parameter.Type);
        }

        [Fact]
        public void FunctionPointerTest2()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
typedef void (*function_pointer_t)(int);
void Test(function_pointer_t function);
"
            );

            // Reduce with the typedef
            {
                TranslatedLibrary reduced = new TypeReductionTransformation().Transform(library);
                
                TranslatedTypedef typedef = reduced.FindDeclaration<TranslatedTypedef>("function_pointer_t");
                AssertVoidIntFunctionPointerType(typedef.UnderlyingType);

                // Sanity check that Test's parameter refers to the typedef
                TranslatedParameter parameter = reduced.FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("function");
                Assert.IsAssignableFrom<TranslatedTypeReference>(parameter.Type);
                TranslatedTypeReference type = (TranslatedTypeReference)parameter.Type;
                Assert.ReferenceEqual(typedef, type.TryResolve(reduced));
            }

            // Reduce without the typedef
            {
                TranslatedLibrary withoutTypedef = new RemoveRemainingTypedefsTransformation().Transform(library);
                withoutTypedef = new TypeReductionTransformation().Transform(withoutTypedef);

                TranslatedParameter parameter = withoutTypedef.FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("function");
                AssertVoidIntFunctionPointerType(parameter.Type);
            }
        }
    }
}
