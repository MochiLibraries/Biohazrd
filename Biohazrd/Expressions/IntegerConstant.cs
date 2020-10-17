namespace Biohazrd.Expressions
{
    public sealed record IntegerConstant : ConstantValue
    {
        public int SizeBits { get; init; }
        public bool IsSigned { get; init; }
        /// <summary>The value of this integer constant</summary>
        /// <remarks>If <see cref="IsSigned"/> is true, this value will be a sign-extended <c>long</c>.</remarks>
        public ulong Value { get; init; }

        public override string ToString()
            => IsSigned ? ((long)Value).ToString() : Value.ToString();
    }
}
