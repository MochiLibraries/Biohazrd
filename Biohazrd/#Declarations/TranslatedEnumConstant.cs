using ClangSharp;
using System;

namespace Biohazrd
{
    public sealed record TranslatedEnumConstant : TranslatedDeclaration
    {
        public ulong Value { get; init; }
        public bool HasExplicitValue { get; init; }
        public bool IsHexValue { get; init; }

        internal TranslatedEnumConstant(TranslatedFile file, EnumConstantDecl enumConstant)
            : base(file, enumConstant)
        {
            Value = enumConstant.Handle.EnumConstantDeclUnsignedValue;

            // Determine how this constant is defined
            IntegerLiteral? integerLiteral = TryGetValueLiteral(enumConstant);

            if (integerLiteral is null)
            {
                // We still might have an explicit value even if the previous check failed since it only looks for basic integer values.
                HasExplicitValue = enumConstant.CursorChildren.Count > 0;
                IsHexValue = false;
            }
            else
            {
                HasExplicitValue = true;
                IsHexValue = integerLiteral.Value.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        private static IntegerLiteral? TryGetValueLiteral(Cursor declaration)
        {
            foreach (Cursor cursor in declaration.CursorChildren)
            {
                // If we found the integer literal, return it
                if (cursor is IntegerLiteral integerLiteral)
                { return integerLiteral; }

                // Check recursively
                IntegerLiteral? ret = TryGetValueLiteral(cursor);
                if (ret is not null)
                { return ret; }
            }

            return null;
        }

        public override string ToString()
            => $"Enum Constant {base.ToString()} = {Value}";
    }
}
