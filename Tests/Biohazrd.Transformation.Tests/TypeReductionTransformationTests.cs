using Biohazrd.CSharp;
using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Common;
using ClangSharp.Interop;
using Xunit;

namespace Biohazrd.Transformation.Tests
{
    public sealed class TypeReductionTransformationTests : BiohazrdTestBase
    {
        private FunctionPointerTypeReference AssertVoidIntFunctionPointerType(TypeReference type)
        {
            Assert.IsType<FunctionPointerTypeReference>(type);
            FunctionPointerTypeReference functionPointer = (FunctionPointerTypeReference)type;

            Assert.IsType<VoidTypeReference>(functionPointer.ReturnType);

            Assert.Single(functionPointer.ParameterTypes);
            Assert.IsType<ClangTypeReference>(functionPointer.ParameterTypes[0]);
            ClangTypeReference parameterType = (ClangTypeReference)functionPointer.ParameterTypes[0];
            Assert.Equal(CXTypeKind.CXType_Int, parameterType.ClangType.Kind);
            return functionPointer;
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

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/115")]
        public void FunctionPointer_PointerToNonPointerFunction()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
typedef void (function_t)(int);
void Test(function_t* function);
"
            );

            // Reduce with the typedef
            {
                TranslatedLibrary reduced = new TypeReductionTransformation().Transform(library);

                TranslatedTypedef typedef = reduced.FindDeclaration<TranslatedTypedef>("function_t");
                FunctionPointerTypeReference functionPointer = AssertVoidIntFunctionPointerType(typedef.UnderlyingType);
                Assert.True(functionPointer.IsNotActuallyAPointer); // The function type should be bare

                // Parameter should reference the typedef via pointer
                TranslatedParameter parameter = reduced.FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("function");
                PointerTypeReference pointerType = Assert.IsType<PointerTypeReference>(parameter.Type);
                TranslatedTypeReference referencedType = Assert.IsAssignableFrom<TranslatedTypeReference>(pointerType.Inner);
                Assert.Equal(typedef, referencedType.TryResolve(reduced));
            }

            // Reduce without the typedef
            {
                TranslatedLibrary withoutTypedef = new RemoveRemainingTypedefsTransformation().Transform(library);
                withoutTypedef = new TypeReductionTransformation().Transform(withoutTypedef);

                TranslatedParameter parameter = withoutTypedef.FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("function");
                FunctionPointerTypeReference functionPointer = AssertVoidIntFunctionPointerType(parameter.Type);
                Assert.False(functionPointer.IsNotActuallyAPointer); // This should be flattened to an actual pointer
            }
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/115")]
        public void FunctionPointer_NonPointerFunctionUsedDirectly()
        {
            // Yes this is legal syntax for Test. It acts like a function pointer.
            TranslatedLibrary library = CreateLibrary
            (@"
typedef void (function_t)(int);
void Test(function_t function);
"
            );

            // Reduce with the typedef
            {
                TranslatedLibrary reduced = new TypeReductionTransformation().Transform(library);

                TranslatedTypedef typedef = reduced.FindDeclaration<TranslatedTypedef>("function_t");
                FunctionPointerTypeReference functionPointer = AssertVoidIntFunctionPointerType(typedef.UnderlyingType);
                Assert.True(functionPointer.IsNotActuallyAPointer); // The function type should be bare

                // Parameter should reference the typedef directly
                TranslatedParameter parameter = reduced.FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("function");
                TranslatedTypeReference referencedType = Assert.IsAssignableFrom<TranslatedTypeReference>(parameter.Type);
                Assert.Equal(typedef, referencedType.TryResolve(reduced));
            }

            // Reduce without the typedef
            {
                TranslatedLibrary withoutTypedef = new RemoveRemainingTypedefsTransformation().Transform(library);
                withoutTypedef = new TypeReductionTransformation().Transform(withoutTypedef);

                TranslatedParameter parameter = withoutTypedef.FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("function");
                FunctionPointerTypeReference functionPointer = AssertVoidIntFunctionPointerType(parameter.Type);
                Assert.True(functionPointer.IsNotActuallyAPointer); // This type should still not actually be a pointer since it isn't
            }
        }

        [Fact]
        public void FunctionPointer_NoCallingConvention()
        {
            TranslatedLibrary library = CreateLibrary(@"void Test(void (*function)(int));", "i386-pc-win32");

            library = new TypeReductionTransformation().Transform(library);

            TranslatedParameter parameter = library.FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("function");
            FunctionPointerTypeReference functionPointerType = AssertVoidIntFunctionPointerType(parameter.Type);
            Assert.NotEqual(CXCallingConv.CXCallingConv_X86StdCall, functionPointerType.CallingConvention);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/115")]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/124")]
        public void FunctionPointer_CallingConvention()
        {
            // This also tests the handling of AttributedType
            TranslatedLibrary library = CreateLibrary(@"void Test(void (__stdcall *function)(int));", "i386-pc-win32");

            library = new TypeReductionTransformation().Transform(library);

            TranslatedParameter parameter = library.FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("function");
            FunctionPointerTypeReference functionPointerType = AssertVoidIntFunctionPointerType(parameter.Type);
            Assert.Equal(CXCallingConv.CXCallingConv_X86StdCall, functionPointerType.CallingConvention);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/124")]
        public void AttributedType_CallingConventionOnTypedef_AllTypedefsRemoved()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
typedef void (*function_pointer_t)(int);
typedef __stdcall function_pointer_t function_pointer_stdcall_t;
void Test(function_pointer_stdcall_t function);
", "i386-pc-win32"
            );

            library = new RemoveRemainingTypedefsTransformation().Transform(library);
            library = new TypeReductionTransformation().Transform(library);

            TranslatedParameter parameter = library.FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("function");
            Assert.Contains(parameter.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("Typedef 'function_pointer_t' was swallowed by attribute"));
            FunctionPointerTypeReference functionPointerType = AssertVoidIntFunctionPointerType(parameter.Type);
            Assert.Equal(CXCallingConv.CXCallingConv_X86StdCall, functionPointerType.CallingConvention);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/124")]
        public void AttributedType_CallingConventionOnTypedef_IntermediateTypedefRemoved()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
typedef void (*function_pointer_t)(int);
typedef __stdcall function_pointer_t function_pointer_stdcall_t;
void Test(function_pointer_stdcall_t function);
", "i386-pc-win32"
            );

            library = library with
            {
                Declarations = library.Declarations.RemoveAll(d => d.Name == "function_pointer_stdcall_t")
            };
            library = new TypeReductionTransformation().Transform(library);

            // Note that we _don't_ expect `function` to reference `function_pointer_t`
            // Biohazrd has no way of representing the attribute without reducing the typedef, so it gets flattened prematurely
            TranslatedParameter parameter = library.FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("function");
            Assert.Contains(parameter.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("Typedef 'function_pointer_t' was swallowed by attribute"));
            FunctionPointerTypeReference functionPointerType = AssertVoidIntFunctionPointerType(parameter.Type);
            Assert.Equal(CXCallingConv.CXCallingConv_X86StdCall, functionPointerType.CallingConvention);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/124")]
        public void AttributedType_CallingConventionOnTypedef_NoTypedefRemoved()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
typedef void (*function_pointer_t)(int);
typedef __stdcall function_pointer_t function_pointer_stdcall_t;
void Test(function_pointer_stdcall_t function);
", "i386-pc-win32"
            );

            library = new TypeReductionTransformation().Transform(library);

            TranslatedParameter parameter = library.FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("function");
            TranslatedTypeReference parameterType = Assert.IsAssignableFrom<TranslatedTypeReference>(parameter.Type);
            TranslatedTypedef parameterTypeTypedef = Assert.IsType<TranslatedTypedef>(parameterType.TryResolve(library));
            Assert.ReferenceEqual(library.FindDeclaration<TranslatedTypedef>("function_pointer_stdcall_t"), parameterTypeTypedef);

            // As with AttributedType_CallingConventionOnTypedef_IntermediateTypedefRemoved, we don't expect `function_pointer_stdcall_t` to reference `function_pointer_t`
            Assert.Contains(parameterTypeTypedef.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("Typedef 'function_pointer_t' was swallowed by attribute"));
            FunctionPointerTypeReference functionPointerType = AssertVoidIntFunctionPointerType(parameterTypeTypedef.UnderlyingType);
            Assert.Equal(CXCallingConv.CXCallingConv_X86StdCall, functionPointerType.CallingConvention);

            // Sanity check that `function_pointer_t` was reduced correctly
            TranslatedTypedef baseTypedef = library.FindDeclaration<TranslatedTypedef>("function_pointer_t");
            Assert.Empty(baseTypedef.Diagnostics);
            AssertVoidIntFunctionPointerType(baseTypedef.UnderlyingType);
        }

        [Fact]
        public void Typedef_RemainingTypedefIsReducedToTypedefReference()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
typedef int MyTypedef;
MyTypedef TestFunction();
"
            );

            library = new TypeReductionTransformation().Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            TranslatedDeclaration? returnType = Assert.IsAssignableFrom<TranslatedTypeReference>(function.ReturnType).TryResolve(library);
            Assert.IsType<TranslatedTypedef>(returnType);
            Assert.Equal("MyTypedef", returnType.Name);
        }

        [Fact]
        public void Typedef_RemovedTypedefIsReducedToAliasedType()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
typedef int MyTypedef;
MyTypedef TestFunction();
"
            );

            library = new RemoveRemainingTypedefsTransformation().Transform(library);
            library = new TypeReductionTransformation().Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.Equal(CXTypeKind.CXType_Int, Assert.IsType<ClangTypeReference>(function.ReturnType).ClangType.Kind);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/122")]
        public void Typedef_RemovedTypedefToReplacedTypedefIsReducedToReplacement()
        {
            TranslatedLibrary library = CreateLibrary
(@"
typedef int MyTypedef;
typedef MyTypedef OtherTypedef;
OtherTypedef TestFunction();
"
);

            library = library with
            {
                Declarations = library.Declarations.RemoveAll(d => d.Name == "OtherTypedef")
            };
            library = new TypeReductionTransformation().Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            TranslatedDeclaration? returnType = Assert.IsAssignableFrom<TranslatedTypeReference>(function.ReturnType).TryResolve(library);
            Assert.IsType<TranslatedTypedef>(returnType);
            Assert.Equal("MyTypedef", returnType.Name);
        }

        [Fact]
        public void Typedef_RemovedTypedefToRemovedTypedefIsReducedToAliasedType()
        {
            TranslatedLibrary library = CreateLibrary
(@"
typedef int MyTypedef;
typedef MyTypedef OtherTypedef;
OtherTypedef TestFunction();
"
);

            library = new RemoveRemainingTypedefsTransformation().Transform(library);
            library = new TypeReductionTransformation().Transform(library);

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("TestFunction");
            Assert.Equal(CXTypeKind.CXType_Int, Assert.IsType<ClangTypeReference>(function.ReturnType).ClangType.Kind);
        }
    }
}
