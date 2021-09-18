using Biohazrd.Transformation;
using Biohazrd.Transformation.Common;
using Biohazrd.Transformation.Infrastructure;
using ClangSharp;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using ClangType = ClangSharp.Type;

namespace Biohazrd.CSharp
{
    public class CSharpTypeReductionTransformation : TypeReductionTransformation
    {
        private ConcurrentDictionary<(ClangType Type, long ElementCount), ConstantArrayTypeDeclaration> ConstantArrayTypes = new();
        private ConcurrentBag<ConstantArrayTypeDeclaration> NewConstantArrayTypes = new();
        private object ConstantArrayTypeCreationLock = new();
        private int ConstantArrayTypesCreated;

        protected override TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
        {
            Debug.Assert(ConstantArrayTypes.Count == 0 && NewConstantArrayTypes.Count == 0, "There should not be any constant array types at this point.");
            ConstantArrayTypes.Clear();
            NewConstantArrayTypes.Clear();
            ConstantArrayTypesCreated = 0;

            // We only look for constant array type declarations at the root of the library since that's were we add them
            foreach (ConstantArrayTypeDeclaration existingDeclaration in library.Declarations.OfType<ConstantArrayTypeDeclaration>())
            {
                bool success = ConstantArrayTypes.TryAdd((existingDeclaration.OriginalClangElementType, existingDeclaration.ElementCount), existingDeclaration);
                Debug.Assert(success, $"{existingDeclaration} must successfully add to the lookup dictionary.");
            }

            return library;
        }

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
        {
            library = library with
            {
                Declarations = library.Declarations.AddRange(NewConstantArrayTypes.OrderBy(t => t.Name))
            };

            ConstantArrayTypesCreated = NewConstantArrayTypes.Count;
            ConstantArrayTypes.Clear();
            NewConstantArrayTypes.Clear();

            return base.PostTransformLibrary(library);
        }

        protected override TranslatedLibrary __HACK__PostPostTransformLibrary(TranslatedLibrary library)
        {
            // Persistently run the transformation to ensure nested constant arrays are generated
            // (This is a workaround for https://github.com/InfectedLibraries/Biohazrd/issues/64)
            library = ConstantArrayTypesCreated > 0 ? Transform(library) : base.__HACK__PostPostTransformLibrary(library);

            // Final pass to fix up function pointer parameters
            library = new FunctionPointerParameterFixupPassTransformation().Transform(library);

            return library;
        }

        private ConstantArrayTypeDeclaration GetOrCreateConstantArrayTypeDeclaration(ConstantArrayType constantArrayType)
        {
            (ClangType Type, long ElementCount) key = (constantArrayType.ElementType, constantArrayType.Size);
            ConstantArrayTypeDeclaration? result;

            // Check if we have a cached instance of the constant array type
            // Note: We cannot use GetOrAdd here because we need to add the value to NewConstantArrayTypes too and it doesn't happen atomically
            if (ConstantArrayTypes.TryGetValue(key, out result))
            { return result; }

            // Acquire the creation lock
            bool addSuccess = false;
            lock (ConstantArrayTypeCreationLock)
            {
                // Check again in case another thread added the type while we were waiting for the lock
                if (ConstantArrayTypes.TryGetValue(key, out result))
                { return result; }

                // Create the new declaration
                result = new ConstantArrayTypeDeclaration(constantArrayType);

                // Add the new declaration to the lookup dictionary
                addSuccess = ConstantArrayTypes.TryAdd(key, result);
            }

            // Finish add actions outside of the lock
            // (We can add to the bag outside the lock because it is only read at the very end of the transformation.)
            Debug.Assert(addSuccess, $"{constantArrayType} should've been added successfully.");
            NewConstantArrayTypes.Add(result);

            // Return the result
            return result;
        }

        protected override TypeTransformationResult TransformClangTypeReference(TypeTransformationContext context, ClangTypeReference type)
        {
            switch (type.ClangType)
            {
                case ConstantArrayType constantArrayType:
                    TypeReference result = GetOrCreateConstantArrayTypeDeclaration(constantArrayType).ThisTypeReference;

                    // From the C++17 spec dcl.fct:
                    // > any parameter of type “array of T” [...] is adjusted to be “pointer to T”.
                    // https://timsong-cpp.github.io/cppwp/n4659/dcl.fct#5
                    if (context.ParentDeclaration is TranslatedParameter)
                    { result = new PointerTypeReference(result); }

                    return result;
                default:
                    return base.TransformClangTypeReference(context, type);
            }
        }

        // This transformation fixes up function pointer types to have the same pass-via-pointer behavior as above
        // (Ideally we don't need to do this, but we don't currently have a great way to disambiguate between a function pointer's return type and a parameter types from the context.)
        private sealed class FunctionPointerParameterFixupPassTransformation : TypeTransformationBase
        {
            protected override TypeTransformationResult TransformFunctionPointerTypeReference(TypeTransformationContext context, FunctionPointerTypeReference type)
            {
                DiagnosticAccumulator diagnostics = new();
                TypeArrayTransformHelper newParameters = new(type.ParameterTypes, ref diagnostics);

                foreach (TypeReference parameterType in type.ParameterTypes)
                {
                    if (parameterType is TranslatedTypeReference translatedTypeReference && translatedTypeReference.TryResolve(context.Library) is ConstantArrayTypeDeclaration)
                    { newParameters.Add(new PointerTypeReference(parameterType)); }
                    else
                    { newParameters.Add(parameterType); }
                }

                Debug.Assert(newParameters.TransformationIsComplete);
                if (newParameters.WasChanged || diagnostics.HasDiagnostics)
                {
                    TypeTransformationResult result = type with
                    {
                        ParameterTypes = newParameters.MoveToImmutable()
                    };
                    result.AddDiagnostics(diagnostics.MoveToImmutable());
                    return result;
                }

                return type;
            }
        }
    }
}
