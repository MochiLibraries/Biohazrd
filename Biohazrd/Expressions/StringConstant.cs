namespace Biohazrd.Expressions
{
    public sealed record StringConstant : ConstantValue
    {
        public string Value { get; init; }

        public StringConstant(string value)
            => Value = value;

        public override string ToString()
            => $"\"{Value.Replace("\"", "\\\"")}\"";
    }
}
