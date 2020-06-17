using ClangSharp;
using System;
using System.Collections.Generic;

namespace ClangSharpTest2020
{
    public sealed class TranslatedEnum : TranslatedDeclaration
    {
        private readonly EnumDecl EnumDeclaration;
        private readonly List<EnumConstant> Values = new List<EnumConstant>();

        public override string TranslatedName => EnumDeclaration.Name;
        public override bool CanBeRoot => true;

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
                    HasExplicitValue = false;
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
            File.Consume(EnumDeclaration);

            // Enumerate all of the values for this enum
            foreach (Cursor cursor in EnumDeclaration.CursorChildren)
            {
                if (cursor is EnumConstantDecl enumConstant)
                { Values.Add(new EnumConstant(File, enumConstant)); }
                else
                { File.Diagnostic(Severity.Warning, cursor, $"Encountered unexpected {cursor.CursorKindDetailed()} cursor in enum declaration."); }
            }
        }

        public override void Translate(CodeWriter writer)
        {
            //TODO
            using var _ = writer.DisableScope(String.IsNullOrEmpty(TranslatedName), File, EnumDeclaration, "Unimplemented translation: Anonymous enum");

            writer.EnsureSeparation();
            writer.Write("enum ");
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
    }
}
