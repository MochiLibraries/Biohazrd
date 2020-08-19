using System;

namespace Biohazrd
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

        public static bool IsSigned(this UnderlyingEnumType type)
        {
            switch (type)
            {
                case UnderlyingEnumType.Byte:
                case UnderlyingEnumType.UShort:
                case UnderlyingEnumType.UInt:
                case UnderlyingEnumType.ULong:
                    return false;
                case UnderlyingEnumType.SByte:
                case UnderlyingEnumType.Short:
                case UnderlyingEnumType.Int:
                case UnderlyingEnumType.Long:
                    return true;
                default:
                    throw new ArgumentException("Invalid underlying enum type specified.", nameof(type));
            }
        }

        public static ulong GetMaxValue(this UnderlyingEnumType type)
            => type switch
            {
                UnderlyingEnumType.Byte => Byte.MaxValue,
                UnderlyingEnumType.UShort => UInt16.MaxValue,
                UnderlyingEnumType.UInt => UInt32.MaxValue,
                UnderlyingEnumType.ULong => UInt64.MaxValue,
                UnderlyingEnumType.SByte => (ulong)SByte.MaxValue,
                UnderlyingEnumType.Short => (ulong)Int16.MaxValue,
                UnderlyingEnumType.Int => Int32.MaxValue,
                UnderlyingEnumType.Long => Int64.MaxValue,
                _ => throw new ArgumentException("Invalid underlying enum type specified.", nameof(type))
            };

        public static int SizeOf(this UnderlyingEnumType type)
            => type switch
            {
                UnderlyingEnumType.Byte => sizeof(byte),
                UnderlyingEnumType.UShort => sizeof(ushort),
                UnderlyingEnumType.UInt => sizeof(uint),
                UnderlyingEnumType.ULong => sizeof(ulong),
                UnderlyingEnumType.SByte => sizeof(sbyte),
                UnderlyingEnumType.Short => sizeof(short),
                UnderlyingEnumType.Int => sizeof(int),
                UnderlyingEnumType.Long => sizeof(long),
                _ => throw new ArgumentException("Invalid underlying enum type specified.", nameof(type))
            };
    }
}
