using Biohazrd.Transformation;
using ClangSharp;
using ClangSharp.Interop;
using static ClangSharp.Interop.CXTypeKind;
using ClangType = ClangSharp.Type;

namespace Biohazrd.CSharp
{
    public sealed class CSharpBuiltinTypeTransformation : TypeTransformationBase
    {
        protected override TypeTransformationResult TransformClangTypeReference(TypeTransformationContext context, ClangTypeReference type)
        {
            ClangType clangType = type.ClangType;

            // We only handle builtin types
            if (clangType is not BuiltinType)
            { return type; }

            static CSharpBuiltinType? GetTypeFromSize(ClangType type, bool isSigned)
                => type.Handle.SizeOf switch
                {
                    1 => isSigned ? CSharpBuiltinType.SByte : CSharpBuiltinType.Byte,
                    2 => isSigned ? CSharpBuiltinType.Short : CSharpBuiltinType.UShort,
                    4 => isSigned ? CSharpBuiltinType.Int : CSharpBuiltinType.UInt,
                    8 => isSigned ? CSharpBuiltinType.Long : CSharpBuiltinType.ULong,
                    _ => null
                };

            CSharpBuiltinType? cSharpType = clangType.Kind switch
            {
                CXType_Bool => CSharpBuiltinType.Bool,

                // Character types
                // We always translate `char` (without an explicit sign) as `byte` because in C this type ususally indicates a string and .NET's Encoding utilities all work with bytes.
                // (Additionally, good developers will explicitly sign numeric 8-bit fields since char's signedness is undefined)
                CXType_Char_S => CSharpBuiltinType.Byte, // char (with -fsigned-char)
                CXType_Char_U => CSharpBuiltinType.Byte, // char (with -fno-signed-char)
                CXType_Char16 => CSharpBuiltinType.Char, // char16_t
                CXType_WChar when (clangType.Handle.SizeOf == 2) => CSharpBuiltinType.Char, // wchar_t

                // Unsigned integer types
                CXType_UChar => CSharpBuiltinType.Byte, // unsigned char / uint8_t
                CXType_UShort => CSharpBuiltinType.UShort,
                CXType_UInt => CSharpBuiltinType.UInt,
                CXType_ULong => GetTypeFromSize(clangType, false),
                CXType_ULongLong => CSharpBuiltinType.ULong,

                // Signed integer types
                CXType_SChar => CSharpBuiltinType.SByte, // signed char / int8_t
                CXType_Short => CSharpBuiltinType.Short,
                CXType_Int => CSharpBuiltinType.Int,
                CXType_Long => GetTypeFromSize(clangType, true),
                CXType_LongLong => CSharpBuiltinType.Long,

                // Floating point types
                CXType_Float => CSharpBuiltinType.Float,
                CXType_Double => CSharpBuiltinType.Double,

                // If we got this far, we don't know how to translate this type
                _ => null
            };

            // If the Clang type is not a C# builtin, no transformation occurs
            if (cSharpType is null)
            { return type; }

            // Build the result
            TypeTransformationResult result = (CSharpBuiltinTypeReference)cSharpType;

            // Sanity check the size of the type
            long typeSize = clangType.Handle.SizeOf;

            if (typeSize < 0)
            {
                CXTypeLayoutError sizeError = (CXTypeLayoutError)typeSize;
                result = result.AddDiagnostic(Severity.Error, $"Failed to sanity check size of '{clangType}' due to {sizeError}");
            }
            else if (typeSize != cSharpType.SizeOf)
            {
                result = result.AddDiagnostic
                (
                    Severity.Error,
                    $"Built-in type size sanity check failed, expected Clang sizeof({clangType}) to be the same as C# sizeof({cSharpType.CSharpKeyword}) ({typeSize} != {cSharpType.SizeOf})"
                );
            }

            return result;
        }
    }
}
