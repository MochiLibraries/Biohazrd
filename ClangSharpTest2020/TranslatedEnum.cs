using ClangSharp;
using ClangSharp.Pathogen;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ClangSharpTest2020
{
    public sealed class TranslatedEnum : TranslatedDeclaration
    {
        internal EnumDecl EnumDeclaration { get; }
        private readonly List<EnumConstant> Values = new List<EnumConstant>();

        public override string DefaultName { get; }

        private readonly bool WasAnonymous = false;
        public override bool CanBeRoot => !WillTranslateAsLooseConstants;
        // Anonyomous enums which are not enum class will be translated as loose constants instead of a normal enum type.
        //TODO: Don't do this when the anonyomous enum is used to type a field. Instead we should get our name from that field. (IE: Name ourselves <FieldName>Enum)
        public bool WillTranslateAsLooseConstants => WasAnonymous && !EnumDeclaration.IsClass;

        public bool IsFlags { get; set; } = false;
        public UnderlyingEnumType UnderlyingType { get; set; }

        private struct EnumConstant
        {
            public readonly EnumConstantDecl Declaration;
            public readonly string Name;
            public readonly ulong Value;
            public readonly bool HasExplicitValue;
            public readonly bool IsHexValue;

            public EnumConstant(TranslatedFile file, EnumConstantDecl declaration)
            {
                Declaration = declaration;
                Name = Declaration.Name.ToString();
                Value = Declaration.GetConstantValueZeroExtended();

                // Determine how this constant is defined
                IntegerLiteral integerLiteral = TryGetValueLiteral(file, declaration);

                if (integerLiteral is null)
                {
                    // We still might have an explicit value even if the previous check failed since it only looks for basic integer values.
                    HasExplicitValue = declaration.CursorChildren.Count > 0;
                    IsHexValue = false;
                }
                else
                {
                    HasExplicitValue = true;
                    IsHexValue = integerLiteral.Value.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase);
                }
            }

            private static IntegerLiteral TryGetValueLiteral(TranslatedFile file, Cursor declaration)
            {
                foreach (Cursor cursor in declaration.CursorChildren)
                {
                    // If we found the integer literal, return it
                    if (cursor is IntegerLiteral integerLiteral)
                    { return integerLiteral; }

                    // Check recursively
                    IntegerLiteral ret = TryGetValueLiteral(file, cursor);
                    if (ret is object)
                    { return ret; }
                }

                return null;
            }
        }

        internal TranslatedEnum(IDeclarationContainer container, EnumDecl enumDeclaration)
            : base(container)
        {
            EnumDeclaration = enumDeclaration;
            Declaration = EnumDeclaration;
            Accessibility = Declaration.Access.ToTranslationAccessModifier();
            UnderlyingType = EnumDeclaration.GetUnderlyingEnumType(File);

            DefaultName = EnumDeclaration.Name;

            if (String.IsNullOrEmpty(DefaultName))
            {
                DefaultName = Parent.GetNameForUnnamed("Enum");
                WasAnonymous = true;
            }

            // Enumerate all of the values for this enum
            foreach (Cursor cursor in EnumDeclaration.CursorChildren)
            {
                if (cursor is EnumConstantDecl enumConstant)
                { Values.Add(new EnumConstant(File, enumConstant)); }
                else
                { File.Diagnostic(Severity.Warning, cursor, $"Encountered unexpected {cursor.CursorKindDetailed()} cursor in enum declaration."); }
            }
        }

        protected override void TranslateImplementation(CodeWriter writer)
        {
            if (WillTranslateAsLooseConstants)
            {
                TranslateAsLooseConstants(writer);
                return;
            }

            writer.EnsureSeparation();

            if (IsFlags)
            {
                writer.Using("System");
                writer.WriteLine("[Flags]");
            }

            writer.Write($"{Accessibility.ToCSharpKeyword()} enum ");
            writer.WriteIdentifier(TranslatedName);

            // If the enum has a integer type besides int, emit the base specifier
            if (UnderlyingType != UnderlyingEnumType.Int)
            { writer.Write($" : {UnderlyingType.ToCSharpKeyword()}"); }

            writer.WriteLine();

            using (writer.Block())
            {
                ulong expectedValue = 0;
                bool first = true;
                foreach (EnumConstant value in Values)
                {
                    // If we aren't the first value, write out the comma and newline for the previous value
                    if (first)
                    { first = false; }
                    else
                    { writer.WriteLine(','); }

                    // Determine if we need to write out the value explicitly
                    bool writeOutValue = false;

                    // If the constant has an explicit value in C++, we'll put one in the translation too.
                    //TODO: It'd be nice if we wrote out the expression that created the value into a (doc?) comment. (It's nice for combined enum flags.)
                    if (value.HasExplicitValue)
                    { writeOutValue = true; }
                    // If the value isn't what we expect, write it out explicitly and warn since we don't expect this to happen.
                    else if (value.Value != expectedValue)
                    {
                        writeOutValue = true;
                        File.Diagnostic
                        (
                            Severity.Warning,
                            value.Declaration,
                            $"{EnumDeclaration.Name}.{value.Declaration.Name} had an implicit value, but it had to be translated with an explicit one."
                        );
                    }

                    // Write out the constant name
                    writer.WriteIdentifier(value.Name);

                    if (writeOutValue)
                    {
                        writer.Write(" = ");
                        TranslateConstantValue(writer, value);
                    }

                    // Determine the expected value of the next constant (assuming it's implicit)
                    expectedValue = value.Value + 1;
                }

                // If constants were written, add the newline for the final constant
                if (!first)
                { writer.WriteLine(); }
            }
        }

        private void TranslateAsLooseConstants(CodeWriter writer)
        {
            writer.EnsureSeparation();

            foreach (EnumConstant value in Values)
            {
                writer.Write($"{Accessibility.ToCSharpKeyword()} const {UnderlyingType.ToCSharpKeyword()} ");
                writer.WriteIdentifier(value.Name);
                writer.Write(" = ");
                TranslateConstantValue(writer, value);
                writer.WriteLine(";");
            }
        }

        private void TranslateConstantValue(CodeWriter writer, EnumConstant value)
        {
            // If the constant value is translated as hex, we can just write it out directly
            if (value.IsHexValue)
            {
                // If the value exceeds the maximum value of the underlying type (happens with hex values of signed numbers) we need to add an unchecked explicit cast.
                bool needsCast = value.Value > UnderlyingType.GetMaxValue();
                if (needsCast)
                { writer.Write($"unchecked(({UnderlyingType.ToCSharpKeyword()})"); }

                writer.Write($"0x{value.Value:X}");

                if (needsCast)
                { writer.Write(')'); }
                return;
            }

            // For unsigned values, we can just write out the value directly
            if (!UnderlyingType.IsSigned())
            {
                writer.Write(value.Value);
                return;
            }

            // For signed values, we need to cast the value to the actual signed type
            switch (UnderlyingType)
            {
                case UnderlyingEnumType.SByte:
                    writer.Write((sbyte)value.Value);
                    return;
                case UnderlyingEnumType.Short:
                    writer.Write((short)value.Value);
                    return;
                case UnderlyingEnumType.Int:
                    writer.Write((int)value.Value);
                    return;
                case UnderlyingEnumType.Long:
                    writer.Write((long)value.Value);
                    return;
            }

            // Fallback (we should never get here unless a new underlying enum type is added that we aren't handling.)
            Debug.Assert(false); // Should never get here since it indicates a signed underlying type that we don't support
            writer.Write($"unchecked(({UnderlyingType.ToCSharpKeyword()}){value.Value})");
        }
    }
}
