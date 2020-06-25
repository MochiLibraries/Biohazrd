using System;

namespace ClangSharpTest2020
{
    public enum UnderlyingEnumType
    {
        Byte,
        SByte,
        Short,
        UShort,
        Int,
        UInt,
        Long,
        ULong,
    }

    public static class UnderlyingEnumTypeEx
    {
        public static string ToCSharpKeyword(this UnderlyingEnumType type)
            => type switch
            {
                UnderlyingEnumType.Byte => "byte",
                UnderlyingEnumType.SByte => "sbyte",
                UnderlyingEnumType.Short => "short",
                UnderlyingEnumType.UShort => "ushort",
                UnderlyingEnumType.Int => "int",
                UnderlyingEnumType.UInt => "uint",
                UnderlyingEnumType.Long => "long",
                UnderlyingEnumType.ULong => "ulong",
                _ => throw new ArgumentException("Invalid underlying enum type specified.", nameof(type))
            };
    }
}
