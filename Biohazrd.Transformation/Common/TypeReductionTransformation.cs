using ClangSharp;
using ClangSharp.Interop;
using System.Diagnostics;

namespace Biohazrd.Transformation.Common
{
    public class TypeReductionTransformation : TypeTransformationBase
    {
        protected override TypeTransformationResult TransformClangTypeReference(TypeTransformationContext context, ClangTypeReference type)
        {
            switch (type.ClangType)
            {
                // Void type
                case BuiltinType { Kind: CXTypeKind.CXType_Void }:
                {
                    return VoidTypeReference.Instance;
                }
                // Elaborated types are namespace-qualified types like physx::PxU32 instead of just PxU32.
                case ElaboratedType elaboratedType:
                {
                    return new ClangTypeReference(elaboratedType.NamedType);
                }
                // We don't care that `auto` or `decltype` was used for a type, so we just replace them with their canonical form
                case AutoType:
                case DecltypeType:
                {
                    return new ClangTypeReference(type.ClangType.CanonicalType);
                }
                // We don't care about attributed types, the attributes will be reflected in the actual type if they're relevant
                case AttributedType attributedType:
                {
                    // This case should be unreachable because we disabled CXTranslationUnit_IncludeAttributedTypes as a workaround for https://github.com/InfectedLibraries/Biohazrd/issues/130
                    // Eventually we want to re-enable the flag because disabling it has unintended side-effects, but we need to properly resolve #130 first.
                    Debug.Fail("This case is thought to be unreachable. See https://github.com/InfectedLibraries/Biohazrd/issues/130");

                    // The use of the canonical type here is intentional because the ModifiedType won't reflect the expected changes from the attribute
                    // Unfortunately this also means things like typedefs are lost, but the alternative is manually modifying things in response to the attribute
                    // and there's simply too many attributes with too many meanings in too many contexts to make that a realistic proposition.
                    TypeTransformationResult result = new ClangTypeReference(attributedType.CanonicalType);

                    // If the immediately modified type is a typedef, we issue a warning. (This isn't perfect, but it's better than nothing.)
                    if (attributedType.ModifiedType is TypedefType modifiedTypedef)
                    {
                        result = result.AddDiagnostic
                        (
                            Severity.Warning,
                            $"Typedef '{modifiedTypedef.Decl.Name}' was swallowed by attribute when reducing type for {context}, reduction may not be exactly as expected."
                        );
                    }

                    return result;
                }
                // If a typedef is mapped to a translated declaration, it becomes a translated type reference.
                // Otherwise we eliminate the typedef
                case TypedefType typedefType:
                {
                    if (context.Library.TryFindTranslation(typedefType.Decl) is TranslatedDeclaration declaration)
                    { return TranslatedTypeReference.Create(declaration); }
                    else
                    { return new ClangTypeReference(typedefType.Decl.UnderlyingType); }
                }
                case PointerType pointerType:
                {
                    ClangTypeReference inner = new(pointerType.PointeeType);
                    return new PointerTypeReference(inner);
                }
                case ReferenceType referenceType:
                {
                    ClangTypeReference inner = new(referenceType.PointeeType);
                    TypeTransformationResult result = new PointerTypeReference(inner)
                    {
                        WasReference = true
                    };

                    if (referenceType is RValueReferenceType)
                    { result = result.AddDiagnostic(Severity.Warning, "Found RValue reference type. This type may not be translated correctly."); }

                    return result;
                }
                // Constant arrays passed as parameters are translated as pointers
                case ConstantArrayType constantArrayType when context.ParentDeclaration is TranslatedParameter:
                {
                    ClangTypeReference inner = new(constantArrayType.ElementType);
                    PointerTypeReference result = new(inner);
                    return new TypeTransformationResult(result, Severity.Warning, $"The size of this constant-sized array parameter was discarded.");
                }
                // Incomplete arrays are translated as pointers
                case IncompleteArrayType incompleteArrayType when (context.ParentDeclaration is TranslatedParameter) || (context.Parent is FunctionPointerTypeReference):
                {
                    ClangTypeReference inner = new(incompleteArrayType.ElementType);
                    return new PointerTypeReference(inner);
                }
                case FunctionProtoType functionProtoType:
                {
                    // Clang represents function pointers as PointerType -> FunctionProtoType, so FunctionProtoType does not actually represent a pointer.
                    // These two types are flattened in a second post-transform pass.
                    return new FunctionPointerTypeReference(functionProtoType, isNotActuallyAPointer: true);
                }
                case EnumType enumType:
                {
                    return TranslatedTypeReference.Create(enumType.Decl);
                }
                case RecordType recordType:
                {
                    return TranslatedTypeReference.Create(recordType.Decl);
                }
                case TemplateSpecializationType templateSpecilaization:
                {
                    // ClangSharp does not surface Declaration on TemplateSpecializationType, but it is supported by libclang:
                    // https://github.com/InfectedLibraries/llvm-project/blob/a6d7a83953a20d699566e0299b9b354511d7cbdf/clang/tools/libclang/CXType.cpp#L507-L513
                    return TranslatedTypeReference.Create((Decl)context.Library.FindClangCursor(templateSpecilaization.Handle.Declaration));
                }
                case SubstTemplateTypeParmType templateSubstitution:
                {
                    // Note that using the CanonicalType here is fine because template specializations always use canonical types.
                    // One might think SubstTemplateTypeParmType::getReplacementType might return the non-canonical type, but internally all it does is return the canonical type.
                    //
                    // This code is not why typedef information is lost in template specializations, it's intrinsic to how Clang handles templates.
                    // See https://github.com/InfectedLibraries/Biohazrd/issues/178 for details.
                    return new ClangTypeReference(templateSubstitution.CanonicalType);
                }
                // Don't know how to reduce this type
                default:
                {
                    return type;
                }
            }
        }

        private FlattenFunctionPointerTransformation FlattenFunctionPointerPass = new();
        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
        {
            library = FlattenFunctionPointerPass.Transform(library);
            return base.PostTransformLibrary(library);
        }

        private class FlattenFunctionPointerTransformation : TypeTransformationBase
        {
            protected override TypeTransformationResult TransformPointerTypeReference(TypeTransformationContext context, PointerTypeReference type)
            {
                // Clang represents function pointers as PointerType -> FunctionProtoType
                // It's fairly uncommon to have a function type that isn't a function pointer type, so we flatten the two
                if (type.Inner is FunctionPointerTypeReference { IsNotActuallyAPointer: true } functionPointerType)
                {
                    return functionPointerType with
                    {
                        IsNotActuallyAPointer = false
                    };
                }
                else
                { return type; }
            }
        }
    }
}
