using System;
using System.Collections.Immutable;

namespace Biohazrd.CSharp
{
    public sealed class CSharpBuiltinType
    {
        public int SizeOf { get; }
        public string CSharpKeyword { get; }

        public bool IsValidUnderlyingEnumType { get; init; }
        public bool IsIntegral { get; init; }
        public bool IsSigned => IsIntegral && MinValue < 0;

        private readonly ulong _MaxValue;
        private readonly long _MinValue;
        public ulong MaxValue
        {
            get => IsIntegral ? _MaxValue : throw new NotSupportedException("You can only get the maximum value of an integral type.");
            init => _MaxValue = value;
        }

        public long MinValue
        {
            get => IsIntegral ? _MinValue : throw new NotSupportedException("You can only get the minimum value of an integral type.");
            init => _MinValue = value;
        }

        internal CSharpBuiltinTypeReference Reference { get; }

        private CSharpBuiltinType(int sizeOf, string cSharpKeyword)
        {
            SizeOf = sizeOf;
            CSharpKeyword = cSharpKeyword;
            Reference = new CSharpBuiltinTypeReference(this);
        }

        public override string ToString()
            => CSharpKeyword;

        public static implicit operator CSharpBuiltinTypeReference(CSharpBuiltinType type)
            => type.Reference;

        public static implicit operator TypeReference(CSharpBuiltinType type)
            => type.Reference;

        //=========================================================================================================================================================================
        // Type definitions
        //=========================================================================================================================================================================
        public static readonly CSharpBuiltinType Byte = new(sizeof(byte), "byte")
        {
            IsValidUnderlyingEnumType = true,
            IsIntegral = true,
            MinValue = byte.MinValue,
            MaxValue = byte.MaxValue
        };

        public static readonly CSharpBuiltinType SByte = new(sizeof(sbyte), "sbyte")
        {
            IsValidUnderlyingEnumType = true,
            IsIntegral = true,
            MinValue = sbyte.MinValue,
            MaxValue = (long)sbyte.MaxValue
        };

        public static readonly CSharpBuiltinType Short = new(sizeof(short), "short")
        {
            IsValidUnderlyingEnumType = true,
            IsIntegral = true,
            MinValue = short.MinValue,
            MaxValue = (long)short.MaxValue
        };

        public static readonly CSharpBuiltinType UShort = new(sizeof(ushort), "ushort")
        {
            IsValidUnderlyingEnumType = true,
            IsIntegral = true,
            MinValue = ushort.MinValue,
            MaxValue = ushort.MaxValue
        };

        public static readonly CSharpBuiltinType Int = new(sizeof(int), "int")
        {
            IsValidUnderlyingEnumType = true,
            IsIntegral = true,
            MinValue = int.MinValue,
            MaxValue = int.MaxValue
        };

        public static readonly CSharpBuiltinType UInt = new(sizeof(uint), "uint")
        {
            IsValidUnderlyingEnumType = true,
            IsIntegral = true,
            MinValue = uint.MinValue,
            MaxValue = uint.MaxValue
        };

        public static readonly CSharpBuiltinType Long = new(sizeof(long), "long")
        {
            IsValidUnderlyingEnumType = true,
            IsIntegral = true,
            MinValue = long.MinValue,
            MaxValue = long.MaxValue
        };

        public static readonly CSharpBuiltinType ULong = new(sizeof(ulong), "ulong")
        {
            IsValidUnderlyingEnumType = true,
            IsIntegral = true,
            MinValue = (long)ulong.MinValue,
            MaxValue = ulong.MaxValue
        };

        public static readonly CSharpBuiltinType Bool = new(sizeof(bool), "bool");
        public static readonly CSharpBuiltinType Char = new(sizeof(char), "char");
        public static readonly CSharpBuiltinType Float = new(sizeof(float), "float");
        public static readonly CSharpBuiltinType Double = new(sizeof(double), "double");

        public static readonly ImmutableArray<CSharpBuiltinType> AllTypes = ImmutableArray.Create
        (
            Byte,
            SByte,
            Short,
            UShort,
            Int,
            UInt,
            Long,
            ULong,
            Bool,
            Char,
            Float,
            Double
        );
    }
}
