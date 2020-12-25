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
                    // Clang represents function pointers as PointerType -> FunctionProtoType whereas we combine the two.
                    if (pointerType.PointeeType is FunctionProtoType innerFunctionProtoType)
                    { return new FunctionPointerTypeReference(innerFunctionProtoType); }

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
                    Debug.Fail("This branch is thought to be unreachable because it is handled in the PointerType branch.");
                    return new FunctionPointerTypeReference(functionProtoType);
                }
                case EnumType enumType:
                {
                    return TranslatedTypeReference.Create(enumType.Decl);
                }
                case RecordType recordType:
                {
                    return TranslatedTypeReference.Create(recordType.Decl);
                }
                // Don't know how to reduce this type
                default:
                {
                    return type;
                }
            }
        }
    }
}
