using ClangSharp;
using ClangSharp.Interop;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Text;
using ClangType = ClangSharp.Type;

namespace Biohazrd
{
    /// <summary>Represents a function pointer.</summary>
    public record FunctionPointerTypeReference : TypeReference
    {
        public CXCallingConv CallingConvention { get; init; }
        public TypeReference ReturnType { get; init; }
        public ImmutableArray<TypeReference> ParameterTypes { get; init; }

        /// <summary>If true, this type reference actually represents a function type that is not a pointer.</summary>
        /// <remarks>
        /// You generally do not need to worry about this property. It exists to support edge case scenarios where a function type is used without it being a pointer
        /// such as the ones described in https://github.com/InfectedLibraries/Biohazrd/issues/115. Situations where this type can actually exist generally still act like
        /// function pointers.
        ///
        /// Biohazrd will generally eliminate function types like this when it is reasonable to do so.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsNotActuallyAPointer { get; init; }

        public FunctionPointerTypeReference()
        {
            ReturnType = VoidTypeReference.Instance;
            ParameterTypes = ImmutableArray<TypeReference>.Empty;
            IsNotActuallyAPointer = false;
        }

        public FunctionPointerTypeReference(FunctionProtoType clangType)
            : this(clangType, isNotActuallyAPointer: false)
        { }

        /// <summary>Creates a function pointer which might not actually be a pointer. (You should generally not use this.)</summary>
        /// <param name="isNotActuallyAPointer">If true, this type reference is actually just a function type and not a function pointer type.</param>
        /// <remarks>
        /// This constructor overload exists to support edge case scenarios where a function type is used without it being a pointer
        /// such as the ones described in https://github.com/InfectedLibraries/Biohazrd/issues/115.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public FunctionPointerTypeReference(FunctionProtoType clangType, bool isNotActuallyAPointer)
        {
            CallingConvention = clangType.CallConv;
            ReturnType = new ClangTypeReference(clangType.ReturnType);
            IsNotActuallyAPointer = isNotActuallyAPointer;

            ImmutableArray<TypeReference>.Builder parameterTypesBuilder = ImmutableArray.CreateBuilder<TypeReference>(clangType.ParamTypes.Count);
            foreach (ClangType parameterType in clangType.ParamTypes)
            {
                parameterTypesBuilder.Add(new ClangTypeReference(parameterType));
            }

            ParameterTypes = parameterTypesBuilder.MoveToImmutable();
        }

        public override string ToString()
        {
            StringBuilder builder = new();
            builder.Append("FuncPtr(");

            bool first = true;
            foreach (TypeReference parameter in ParameterTypes)
            {
                if (first)
                { first = false; }
                else
                { builder.Append(", "); }

                builder.Append(parameter.ToString());
            }

            builder.Append($") -> {ReturnType}");

            return builder.ToString();
        }
    }
}
