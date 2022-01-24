using Biohazrd.Tests.Common;
using Xunit;

namespace Biohazrd.CSharp.Tests
{
    public sealed class ResolveTypedefsTransformationTests : BiohazrdTestBase
    {
        [Fact]
        public void BasicOperation()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
typedef int MyTypedef;
MyTypedef MyFunction(MyTypedef* x);
"
            );

            library = new CSharpTypeReductionTransformation().Transform(library);
            {
                // Precondition sanity checks
                TranslatedTypedef typedef = library.FindDeclaration<TranslatedTypedef>("MyTypedef");
                CSharpBuiltinTypeReference typedefType = Assert.IsType<CSharpBuiltinTypeReference>(typedef.UnderlyingType);
                Assert.Equal(CSharpBuiltinType.Int, typedefType.Type);

                TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("MyFunction");
                TranslatedTypeReference returnType = Assert.IsAssignableFrom<TranslatedTypeReference>(function.ReturnType);
                Assert.ReferenceEqual(typedef, returnType.TryResolve(library));

                TranslatedParameter parameter = function.FindDeclaration<TranslatedParameter>("x");
                PointerTypeReference pointerType = Assert.IsType<PointerTypeReference>(parameter.Type);
                TranslatedTypeReference parameterType = Assert.IsAssignableFrom<TranslatedTypeReference>(pointerType.Inner);
                Assert.ReferenceEqual(typedef, parameterType.TryResolve(library));
            }

            library = new ResolveTypedefsTransformation().Transform(library);
            {
                Assert.DoesNotContain(library, d => d is TranslatedTypedef);

                TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("MyFunction");
                CSharpBuiltinTypeReference returnType = Assert.IsType<CSharpBuiltinTypeReference>(function.ReturnType);
                Assert.Equal(CSharpBuiltinType.Int, returnType.Type);

                TranslatedParameter parameter = function.FindDeclaration<TranslatedParameter>("x");
                PointerTypeReference pointerType = Assert.IsType<PointerTypeReference>(parameter.Type);
                CSharpBuiltinTypeReference parameterType = Assert.IsType<CSharpBuiltinTypeReference>(pointerType.Inner);
                Assert.Equal(CSharpBuiltinType.Int, parameterType.Type);
            }
        }

        [Fact]
        public void TypedefToTypedefTest()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
typedef int MyTypedef1;
typedef MyTypedef1 MyTypedef2;
MyTypedef2 MyFunction(MyTypedef2* x);
"
            );

            library = new CSharpTypeReductionTransformation().Transform(library);
            library = new ResolveTypedefsTransformation().Transform(library);
            {
                Assert.DoesNotContain(library, d => d is TranslatedTypedef);

                TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("MyFunction");
                CSharpBuiltinTypeReference returnType = Assert.IsType<CSharpBuiltinTypeReference>(function.ReturnType);
                Assert.Equal(CSharpBuiltinType.Int, returnType.Type);

                TranslatedParameter parameter = function.FindDeclaration<TranslatedParameter>("x");
                PointerTypeReference pointerType = Assert.IsType<PointerTypeReference>(parameter.Type);
                CSharpBuiltinTypeReference parameterType = Assert.IsType<CSharpBuiltinTypeReference>(pointerType.Inner);
                Assert.Equal(CSharpBuiltinType.Int, parameterType.Type);
            }
        }
    }
}
