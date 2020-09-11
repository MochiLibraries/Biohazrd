using ClangSharp;
using ClangSharp.Interop;
using System.Collections.Immutable;
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

        public FunctionPointerTypeReference()
        {
            ReturnType = VoidTypeReference.Instance;
            ParameterTypes = ImmutableArray<TypeReference>.Empty;
        }

        public FunctionPointerTypeReference(FunctionProtoType clangType)
        {
            CallingConvention = clangType.CallConv;
            ReturnType = new ClangTypeReference(clangType.ReturnType);

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
