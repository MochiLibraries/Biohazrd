using ClangSharp;
using System;
using System.Collections.Generic;

namespace ClangSharpTest2020
{
    public sealed class TranslatedEnum : TranslatedDeclaration
    {
        internal EnumDecl EnumDeclaration { get; }
        private readonly List<EnumConstant> Values = new List<EnumConstant>();

        public override string TranslatedName { get; }

        private readonly bool WasAnonymous = false;
        public override bool CanBeRoot => !WillTranslateAsLooseConstants;
        // Anonyomous enums which are not enum class will be translated as loose constants instead of a normal enum type.
        //TODO: Don't do this when the anonyomous enum is used to type a field. Instead we should get our name from that field. (IE: Name ourselves <FieldName>Enum)
        public bool WillTranslateAsLooseConstants => WasAnonymous && !EnumDeclaration.IsClass;

        private struct EnumConstant
        {
            public readonly EnumConstantDecl Declaration;
            public readonly string Name;
            public readonly long Value;
            public readonly bool HasExplicitValue;
            public readonly bool IsHexValue;

            public EnumConstant(TranslatedFile file, EnumConstantDecl declaration)
            {
                Declaration = declaration;
                Name = Declaration.Name.ToString();
                Value = Declaration.InitVal;

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
                    // Consume cursors we expect to find under an EnumConstantDecl
                    if (declaration is IntegerLiteral || declaration is CastExpr)
                    { file.Consume(declaration); }

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
            File.Consume(EnumDeclaration);

            TranslatedName = EnumDeclaration.Name;

            if (String.IsNullOrEmpty(TranslatedName))
            {
                TranslatedName = Parent.GetNameForUnnamed("Enum");
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
            writer.Write($"{Accessibility.ToCSharpKeyword()} enum ");
            writer.WriteIdentifier(TranslatedName);

            // If the enum has a integer type besides int, emit the base specifier
            if (EnumDeclaration.IntegerType.Kind != ClangSharp.Interop.CXTypeKind.CXType_Int)
            {
                writer.Write(" : ");
                File.WriteType(writer, EnumDeclaration.IntegerType, EnumDeclaration, TypeTranslationContext.ForEnumUnderlyingType);
            }

            writer.WriteLine();

            using (writer.Block())
            {
                long expectedValue = 0;
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

                        if (value.IsHexValue)
                        { writer.Write($"0x{value.Value:X}"); }
                        else
                        { writer.Write(value.Value); }
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
                writer.Write($"{Accessibility.ToCSharpKeyword()} const ");
                File.WriteType(writer, EnumDeclaration.IntegerType, value.Declaration, TypeTranslationContext.ForEnumUnderlyingType);
                writer.Write(" ");
                writer.WriteIdentifier(value.Name);
                writer.Write(" = ");

                if (value.IsHexValue)
                { writer.Write($"0x{value.Value:X}"); }
                else
                { writer.Write(value.Value); }

                writer.WriteLine(";");
            }
        }
    }
}
