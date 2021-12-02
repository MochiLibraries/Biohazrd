namespace Biohazrd.Expressions
{
    public sealed record IntegerConstant : ConstantValue
    {
        public int SizeBits { get; init; }
        public bool IsSigned { get; init; }
        /// <summary>The value of this integer constant</summary>
        /// <remarks>If <see cref="IsSigned"/> is true, this value will be a sign-extended <c>long</c>.</remarks>
        public ulong Value { get; init; }

        public long SignedValue => (long)Value;

        public override string ToString()
            => IsSigned ? ((long)Value).ToString() : Value.ToString();

        private unsafe static IntegerConstant From<TNumeric>(ulong value, bool isSigned)
            where TNumeric : unmanaged
            => new()
            {
                SizeBits = sizeof(TNumeric) * 8,
                IsSigned = isSigned,
                Value = value
            };

        public static IntegerConstant FromInt64(long value) => From<long>((ulong)value, true);
        public static IntegerConstant FromUInt64(ulong value) => From<ulong>(value, false);
        public static IntegerConstant FromInt32(int value) => From<int>((ulong)value, true);
        public static IntegerConstant FromUInt32(uint value) => From<uint>(value, false);
        public static IntegerConstant FromInt16(short value) => From<short>((ulong)value, true);
        public static IntegerConstant FromUInt16(ushort value) => From<ushort>(value, false);
        public static IntegerConstant FromSByte(sbyte value) => From<sbyte>((ulong)value, true);
        public static IntegerConstant FromByte(byte value) => From<byte>(value, false);
    }
}
