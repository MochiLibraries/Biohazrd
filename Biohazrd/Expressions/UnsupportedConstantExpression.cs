namespace Biohazrd.Expressions
{
    public sealed record UnsupportedConstantExpression : ConstantValue
    {
        public string Message { get; init; }

        internal UnsupportedConstantExpression(string message)
            => Message = message;

        public override string ToString()
            => Message;
    }
}
