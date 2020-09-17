using System.Diagnostics;

namespace Biohazrd.CSharp
{
    partial class CSharpLibraryGenerator
    {
        protected override void VisitEnum(VisitorContext context, TranslatedEnum declaration)
        {
            if (declaration.UnderlyingType is not CSharpBuiltinTypeReference { Type: CSharpBuiltinType underlyingType } || !underlyingType.IsValidUnderlyingEnumType)
            {
                Fatal(context, declaration, "The underlying type is invalid", $": {declaration.UnderlyingType}");
                return;
            }

            if (declaration.TranslateAsLooseConstants)
            {
                Writer.EnsureSeparation();

                foreach (TranslatedEnumConstant value in declaration.Values)
                {
                    Writer.Write($"{value.Accessibility.ToCSharpKeyword()} const {underlyingType.CSharpKeyword} ");
                    Writer.WriteIdentifier(value.Name);
                    Writer.Write(" = ");
                    EmitEnumValue(underlyingType, value);
                    Writer.WriteLine(";");
                }
            }
            else
            {
                Writer.EnsureSeparation();

                if (declaration.IsFlags)
                {
                    Writer.Using("System");
                    Writer.WriteLine("[Flags]");
                }

                Writer.Write($"{declaration.Accessibility.ToCSharpKeyword()} enum ");
                Writer.WriteIdentifier(declaration.Name);

                // If the enum has a integer type besides int, emit the base specifier
                if (underlyingType != CSharpBuiltinType.Int)
                { Writer.Write($" : {underlyingType.CSharpKeyword}"); }

                Writer.WriteLine();

                using (Writer.Block())
                {
                    ulong expectedValue = 0;
                    bool first = true;
                    foreach (TranslatedEnumConstant value in declaration.Values)
                    {
                        // If we aren't the first value, write out the comma and newline for the previous value
                        if (first)
                        { first = false; }
                        else
                        { Writer.WriteLine(','); }

                        // Determine if we need to write out the value explicitly
                        bool writeOutValue = false;

                        // If the constant has an explicit value in C++, we'll put one in the translation too.
                        if (value.HasExplicitValue)
                        { writeOutValue = true; }
                        // If the value isn't what we expect, write it out explicitly and warn since we don't expect this to happen.
                        else if (value.Value != expectedValue)
                        {
                            writeOutValue = true;
                            Diagnostics.Add(Severity.Warning, $"{declaration.Name}.{value.Name} had an implicit value, but it had to be translated with an explicit one.");
                        }

                        // Write out the constant name
                        Writer.WriteIdentifier(value.Name);

                        if (writeOutValue)
                        {
                            Writer.Write(" = ");
                            EmitEnumValue(underlyingType, value);
                        }

                        // Determine the expected value of the next constant (assuming it's implicit)
                        expectedValue = value.Value + 1;
                    }

                    // If constants were written, add the newline for the final constant
                    if (!first)
                    { Writer.WriteLine(); }
                }
            }
        }

        private void EmitEnumValue(CSharpBuiltinType type, TranslatedEnumConstant value)
        {
            // If the constant value is translated as hex, we can just write it out directly
            if (value.IsHexValue)
            {
                // If the value exceeds the maximum value of the underlying type (happens with hex values of signed numbers) we need to add an unchecked explicit cast.
                bool needsCast = value.Value > type.MaxValue;
                if (needsCast)
                { Writer.Write($"unchecked(({type.CSharpKeyword})"); }

                Writer.Write($"0x{value.Value:X}");

                if (needsCast)
                { Writer.Write(')'); }
                return;
            }

            // For unsigned values, we can just write out the value directly
            if (!type.IsSigned)
            {
                Writer.Write(value.Value);
                return;
            }

            // For signed values, we need to cast the value to the actual signed type
            if (type == CSharpBuiltinType.SByte)
            { Writer.Write((sbyte)value.Value); }
            else if (type == CSharpBuiltinType.Short)
            { Writer.Write((short)value.Value); }
            else if (type == CSharpBuiltinType.Int)
            { Writer.Write((int)value.Value); }
            else if (type == CSharpBuiltinType.Long)
            { Writer.Write((long)value.Value); }
            else
            {
                // Fallback (we should never get here unless a new underlying enum type is added that we aren't handling.)
                Debug.Fail("Underlying type-specific enum constant value emit failed.");
                Writer.Write($"unchecked(({type.CSharpKeyword}){value.Value})");
            }
        }

        protected override void VisitEnumConstant(VisitorContext context, TranslatedEnumConstant declaration)
            => FatalContext(context, declaration, $"= {declaration.Value}");
    }
}
