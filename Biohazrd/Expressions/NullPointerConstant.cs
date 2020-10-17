namespace Biohazrd.Expressions
{
    public sealed record NullPointerConstant : ConstantValue
    {
        private NullPointerConstant()
        { }

        public static readonly NullPointerConstant Instance = new();

        public override string ToString()
            => "<null>";
    }
}
