using Biohazrd.OutputGeneration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Biohazrd.CSharp
{
    [ProvidesOutputSessionFactory]
    public class CSharpCodeWriter : CLikeCodeWriter
    {
        private readonly SortedSet<string> UsingNamespaces = new SortedSet<string>(StringComparer.InvariantCulture);

        protected CSharpCodeWriter(OutputSession session, string filePath)
            : base(session, filePath)
        { }

        private static OutputSession.WriterFactory<CSharpCodeWriter> FactoryMethod => (session, filePath) => new CSharpCodeWriter(session, filePath);

        public void Using(string @namespace)
            => UsingNamespaces.Add(@namespace);

        public LeftAdjustedScope DisableScope(bool disabled, string? message)
        {
            if (!disabled)
            { return default; }

            EnsureSeparation();

            LeftAdjustedScope ret;

            if (message is null)
            { ret = CreateLeftAdjustedScope("#if false", "#endif"); }
            else
            { ret = CreateLeftAdjustedScope($"#if false // {message}", "#endif"); }

            NoSeparationNeededBeforeNextLine();
            return ret;
        }

        public LeftAdjustedScope DisableScope(bool disabled)
            => DisableScope(disabled, null);

        public LeftAdjustedScope DisableScope(string message)
            => DisableScope(true, message);

        public LeftAdjustedScope DisableScope()
            => DisableScope(true, null);

        public void WriteIdentifier(string identifier)
            => Write(SanitizeIdentifier(identifier));

        /// <remarks>Identifiers containing Unicode escape sequences (IE: <c>\u0057\u0048\u0059</c>) are not supported and will be sanitized away.</remarks>
        public static string SanitizeIdentifier(string identifier)
        {
            switch (identifier)
            {
                case "abstract":
                case "as":
                case "base":
                case "bool":
                case "break":
                case "byte":
                case "case":
                case "catch":
                case "char":
                case "checked":
                case "class":
                case "const":
                case "continue":
                case "decimal":
                case "default":
                case "delegate":
                case "do":
                case "double":
                case "else":
                case "enum":
                case "event":
                case "explicit":
                case "extern":
                case "false":
                case "finally":
                case "fixed":
                case "float":
                case "for":
                case "foreach":
                case "goto":
                case "if":
                case "implicit":
                case "in":
                case "int":
                case "interface":
                case "internal":
                case "is":
                case "lock":
                case "long":
                case "namespace":
                case "new":
                case "null":
                case "object":
                case "operator":
                case "out":
                case "override":
                case "params":
                case "private":
                case "protected":
                case "public":
                case "readonly":
                case "ref":
                case "return":
                case "sbyte":
                case "sealed":
                case "short":
                case "sizeof":
                case "stackalloc":
                case "static":
                case "string":
                case "struct":
                case "switch":
                case "this":
                case "throw":
                case "true":
                case "try":
                case "typeof":
                case "uint":
                case "ulong":
                case "unchecked":
                case "unsafe":
                case "ushort":
                case "using":
                case "virtual":
                case "void":
                case "volatile":
                case "while":
                    return "@" + identifier;
                default:
                    return SanitizeNonKeywordIdentifier(identifier);
            }
        }

        /// <remarks>Identifiers containing Unicode escape sequences (IE: <c>\u0057\u0048\u0059</c>) are not supported.</remarks>
        public static bool IsLegalIdentifier(string identifier)
        {
            // Relevant spec: https://github.com/dotnet/csharplang/blob/ca09fc178fb0e8285e80d2244786e99e04eed882/spec/lexical-structure.md#identifiers
            if (identifier.Length == 0)
            { return false; }

            if (!IsValidIdentifierStartCharacter(identifier[0]))
            { return false; }

            for (int i = 1; i < identifier.Length; i++)
            {
                if (!IsValidIdentifierCharacter(identifier[i]))
                { return false; }
            }

            // If we got this far, all characters in the identifier are valid.
            return true;
        }

        /// <remarks>Identifiers containing Unicode escape sequences (IE: <c>\u0057\u0048\u0059</c>) are not supported.</remarks>
        private static string SanitizeNonKeywordIdentifier(string identifier)
        {
            // Relevant spec: https://github.com/dotnet/csharplang/blob/ca09fc178fb0e8285e80d2244786e99e04eed882/spec/lexical-structure.md#identifiers
            if (String.IsNullOrEmpty(identifier))
            { throw new ArgumentException("The specified identifier is null or empty.", nameof(identifier)); }

            StringBuilder? ret = null;

            static string Escaped(char c)
                => $"__UNICODE_{((short)c):X4}__";

            if (!IsValidIdentifierStartCharacter(identifier[0]))
            {
                // (Capacity is +32 as a guess that most identifiers won't need more than 2 character replacements.)
                ret = new StringBuilder(identifier.Length + 32);
                ret.Append(Escaped(identifier[0]));
            }

            for (int i = 1; i < identifier.Length; i++)
            {
                char character = identifier[i];

                if (!IsValidIdentifierCharacter(character))
                {
                    // If this is the first replacement, initialize the StringBuilder
                    // (Capacity is +32 as a guess that most identifiers won't need more than 2 character replacements.)
                    if (ret is null)
                    { ret = new StringBuilder(identifier, 0, i, identifier.Length + 32); }

                    ret.Append(Escaped(character));
                }
                else if (ret is not null)
                { ret.Append(character); }
            }

            return ret is not null ? ret.ToString() : identifier;
        }

        /// <summary>Checks that the specified character is a valid <c>identifier_start_character</c>.</summary>
        private static bool IsValidIdentifierStartCharacter(char c)
        {
            if (c == '_')
            { return true; }

            switch (Char.GetUnicodeCategory(c))
            {
                // letter_character
                case UnicodeCategory.UppercaseLetter: // Lu
                case UnicodeCategory.LowercaseLetter: // Ll
                case UnicodeCategory.TitlecaseLetter: // Lt
                case UnicodeCategory.ModifierLetter: // Lm
                case UnicodeCategory.OtherLetter: // Lo
                case UnicodeCategory.LetterNumber: // Nl
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Checks whether the specified character is a valid <c>identifier_part_character</c>.</summary>
        private static bool IsValidIdentifierCharacter(char c)
        {
            switch (Char.GetUnicodeCategory(c))
            {
                // letter_character
                case UnicodeCategory.UppercaseLetter: // Lu
                case UnicodeCategory.LowercaseLetter: // Ll
                case UnicodeCategory.TitlecaseLetter: // Lt
                case UnicodeCategory.ModifierLetter: // Lm
                case UnicodeCategory.OtherLetter: // Lo
                case UnicodeCategory.LetterNumber: // Nl
                // decimal_digit_character
                case UnicodeCategory.DecimalDigitNumber: // Nd
                // connecting_character
                case UnicodeCategory.ConnectorPunctuation: // Pc
                // combining_character
                case UnicodeCategory.NonSpacingMark: // Mn
                case UnicodeCategory.SpacingCombiningMark: // Mc
                // formatting_character
                case UnicodeCategory.Format: // Cf
                    return true;
                default:
                    return false;
            }
        }

        public static string SanitizeStringLiteral(string value)
        {
            // Relevant spec: https://github.com/dotnet/csharplang/blob/0e365431d7ac2a6250089be9e77728ba2742d450/spec/lexical-structure.md#string-literals
            StringBuilder? ret = null;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];

                // Basic replacements
                string? replacement = character switch
                {
                    // Forbidden by single_regular_string_literal_character
                    '\\' => @"\\", // Backslash
                    '"' => "\\\"", // Double quote
                    // new_line_character
                    '\n' => @"\n", // Line feed
                    '\r' => @"\r", // Carriage return
                    '\x0085' => @"\x0085", // Next line character (NEL)
                    '\x2028' => @"\x2028", // Unicode line separator
                    '\x2029' => @"\x2029", // Unicode paragraph separator
                    // Not forbidden, but these are unprintable simple_escape_sequence values not covered above
                    '\0' => @"\0", // Null
                    '\a' => @"\a", // Alert
                    '\b' => @"\b", // Backspace
                    '\f' => @"\f", // Form feed
                    '\t' => @"\t", // Tab
                    '\v' => @"\v", // Vertical tab

                    _ => null
                };

                // More complex replacements
                // Technically the compiler should gladly parse a file with any of these in a string literal, but they have the potential to make the string hard to read in an editor.
                // (Although on the flip side, some of these are printable when used in the correct context. So this might mangle certain non-English text.)
                if (replacement is null)
                {
                    switch (CharUnicodeInfo.GetUnicodeCategory(character))
                    {
                        case UnicodeCategory.Control:
                        case UnicodeCategory.Format:
                        case UnicodeCategory.LineSeparator:
                        case UnicodeCategory.ModifierLetter:
                        case UnicodeCategory.ModifierSymbol:
                        case UnicodeCategory.NonSpacingMark:
                        case UnicodeCategory.OtherNotAssigned:
                        case UnicodeCategory.ParagraphSeparator:
                        case UnicodeCategory.PrivateUse:
                        case UnicodeCategory.SpacingCombiningMark:
                        case UnicodeCategory.Surrogate:
                        {
                            ushort codePoint = (ushort)character;
                            replacement = $"\\x{codePoint:X4}";
                        }
                        break;
                    }
                }

                // If we have no replacement nor string builder, just keep checking
                if (replacement is null && ret is null)
                { continue; }

                // If this is the first replacement, initialize the StringBuilder
                // (Capacity is +10 as a guess that most strings won't need more than 10 basic character replacements.)
                if (ret is null)
                { ret = new StringBuilder(value, 0, i, value.Length + 10); }

                // Append the character or the replacement
                if (replacement is null)
                { ret.Append(replacement); }
                else
                { ret.Append(character); }
            }

            return ret?.ToString() ?? value;
        }

        public static string SanitizeMultiLineComment(string comment)
            // Break */ with a zero-width space so it reads correctly but doesn't end the comment
            => comment.Replace("*/", "*\x200B/");

        protected override void WriteBetweenHeaderAndCode(StreamWriter writer)
        {
            foreach (string usingNamespace in UsingNamespaces)
            { writer.WriteLine($"using {usingNamespace};"); }

            if (UsingNamespaces.Count > 0)
            { writer.WriteLine(); }
        }

        protected override void WriteOutHeaderComment(StreamWriter writer)
        {
            // Roslyn has special understanding of the <auto-generated> comment to indicate the file should not be auto-formatted or receive code analysis warnings
            // Unfortunately another part of this special understanding is that it automatically disables nullability even if it's enabled project-wide
            // Biohazrd does not generally generate anything that needs nullable annotations, but custom declarations might, so we enable it by default
            // See https://github.com/InfectedLibraries/Biohazrd/issues/149
            writer.WriteLine("// <auto-generated>");
            base.WriteOutHeaderComment(writer);
            writer.WriteLine("// </auto-generated>");
            writer.WriteLine("#nullable enable");
        }
    }
}
