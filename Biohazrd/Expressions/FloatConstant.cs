namespace Biohazrd.Expressions
{
    public sealed record FloatConstant : ConstantValue
    {
        public float Value { get; init; }

        public FloatConstant(float value)
            => Value = value;

        public override string ToString()
            => Value.ToString("G9");
    }
}
