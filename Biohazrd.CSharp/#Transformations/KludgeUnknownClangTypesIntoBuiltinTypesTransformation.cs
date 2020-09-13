using Biohazrd.Transformation;
using ClangSharp.Interop;
using System.Collections.Immutable;

namespace Biohazrd.CSharp
{
    public sealed class KludgeUnknownClangTypesIntoBuiltinTypesTransformation : TypeTransformationBase
    {
        private readonly bool EmitErrorOnFail;
        private static ImmutableArray<CSharpBuiltinType> ApplicableBuiltins = ImmutableArray.Create
        (
            CSharpBuiltinType.Byte,
            CSharpBuiltinType.UShort,
            CSharpBuiltinType.UInt,
            CSharpBuiltinType.ULong
        );

        public KludgeUnknownClangTypesIntoBuiltinTypesTransformation(bool emitErrorOnFail)
            => EmitErrorOnFail = emitErrorOnFail;

        protected override TypeTransformationResult TransformClangTypeReference(TypeTransformationContext context, ClangTypeReference type)
        {
            // Get the size of the Clang type
            long sizeOf = type.ClangType.Handle.SizeOf;

            // If the size is negative, Clang could not determine the size of the type
            // No transformation occurs.
            if (sizeOf < 0)
            {
                if (EmitErrorOnFail)
                {
                    CXTypeLayoutError error = (CXTypeLayoutError)sizeOf;
                    return new TypeTransformationResult(type, Severity.Error, $"Kludge failure: Could not determine the size of '{type}' due to {error} error.");
                }

                return type;
            }

            // Look for an unsigned C# builtin type that matches this one in size
            foreach (CSharpBuiltinType builtin in ApplicableBuiltins)
            {
                if (builtin.SizeOf == sizeOf)
                { return new TypeTransformationResult(builtin, Severity.Warning, $"Type '{type}' could not be transformed and was kludged into a {builtin}."); }
            }

            // If we got this far, the type does not fit into any C# builtins
            if (EmitErrorOnFail)
            { return new TypeTransformationResult(type, Severity.Error, $"Kludge failure: '{type}' of size {sizeOf} could not be kludged into any C# built-in types."); }

            return type;
        }
    }
}
