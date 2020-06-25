using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace ClangSharpTest2020
{
    public class CodeWriter : TextWriter
    {
        private const int IndentSize = 4;
        private int IndentLevel = 0;
        private bool OnNewLine = true;
        private string LinePrefix = null;
        private bool NoSeparationNeeded = true;
        private readonly StringBuilder CodeBuilder = new StringBuilder();
        private readonly SortedSet<string> UsingNamespaces = new SortedSet<string>(StringComparer.InvariantCulture);

        public override Encoding Encoding => Encoding.Unicode;

        public void Using(string @namespace)
            => UsingNamespaces.Add(@namespace);

        private void DoIndent()
            => IndentLevel++;

        private void DoUnindent()
        {
            if (IndentLevel <= 0)
            { throw new InvalidOperationException("Can't unindent past level 0!"); }

            IndentLevel--;
        }

        public IndentScope Indent()
            => new IndentScope(this);

        public void NoSeparationNeededBeforeNextLine()
            => NoSeparationNeeded = true;

        public IndentScope Block()
        {
            var ret = new IndentScope(this, "{", "}");
            NoSeparationNeededBeforeNextLine();
            return ret;
        }

        public readonly struct IndentScope : IDisposable
        {
            private readonly CodeWriter Writer;
            private readonly int ExpectedIndentLevel;
            private readonly string EndLine;

            internal IndentScope(CodeWriter writer, string startLine, string endLine)
            {
                if (writer == null)
                { throw new ArgumentNullException(nameof(writer)); }

                Writer = writer;

                if (startLine != null)
                { Writer.WriteLine(startLine); }

                Writer.DoIndent();
                ExpectedIndentLevel = Writer.IndentLevel;
                EndLine = endLine;
            }

            internal IndentScope(CodeWriter writer)
                : this(writer, null, null)
            { }

            void IDisposable.Dispose()
            {
                if (Writer == null)
                { return; }

                if (Writer.IndentLevel != ExpectedIndentLevel)
                { throw new InvalidOperationException("Indent level is not where it should be to close this scope!"); }

                Writer.DoUnindent();

                if (EndLine != null)
                { Writer.WriteLine(EndLine); }
            }
        }

        public void WriteLineLeftAdjusted(string value)
        {
            if (!OnNewLine)
            { throw new InvalidOperationException("Cannot write a left-adjusted line when the current line already contains text."); }

            int oldIndentLevel = IndentLevel;
            try
            {
                IndentLevel = 0;
                WriteLine(value);
            }
            finally
            { IndentLevel = oldIndentLevel; }
        }

        public LeftAdjustedScope DisableScope(bool disabled, TranslatedFile file, ClangSharp.Interop.CXCursor context, string message)
        {
            if (!disabled)
            { return default; }

            EnsureSeparation();

            LeftAdjustedScope ret;

            if (message is null)
            { ret = new LeftAdjustedScope(this, "#if false", "#endif"); }
            else
            {
                file.Diagnostic(Severity.Ignored, context, message);
                ret = new LeftAdjustedScope(this, $"#if false // {message}", "#endif");
            }

            NoSeparationNeededBeforeNextLine();
            return ret;
        }

        public LeftAdjustedScope DisableScope(bool disabled, TranslatedFile file, ClangSharp.Cursor context, string message)
            => DisableScope(disabled, file, context.Handle, message);

        public readonly struct LeftAdjustedScope : IDisposable
        {
            private readonly CodeWriter Writer;
            private readonly int ExpectedIndentLevel;
            private readonly string EndLine;

            internal LeftAdjustedScope(CodeWriter writer, string startLine, string endLine)
            {
                Writer = writer;
                ExpectedIndentLevel = Writer.IndentLevel;
                EndLine = endLine;

                Writer?.WriteLineLeftAdjusted(startLine);
            }

            void IDisposable.Dispose()
            {
                if (Writer is null)
                { return; }

                if (Writer.IndentLevel != ExpectedIndentLevel)
                { throw new InvalidOperationException("Indent level is not where it should be to close this scope!"); }

                Writer.WriteLineLeftAdjusted(EndLine);
            }
        }

        public PrefixScope Prefix(string prefix)
            => new PrefixScope(this, prefix);

        public readonly struct PrefixScope : IDisposable
        {
            private readonly CodeWriter Writer;
            private readonly string MyPrefix;
            private readonly string OldPrefix;

            internal PrefixScope(CodeWriter writer, string prefix)
            {
                if (prefix.Contains('\r') || prefix.Contains('\n'))
                { throw new ArgumentException("The prefix must not contain new lines!", nameof(prefix)); }

                Writer = writer;
                MyPrefix = prefix;
                OldPrefix = Writer.LinePrefix;
                Writer.LinePrefix = prefix;
            }

            void IDisposable.Dispose()
            {
                if (Writer is null)
                { return; }

                if (Writer.LinePrefix != MyPrefix)
                { throw new InvalidOperationException("Prefix is not what it should be to close this scope!"); }

                Writer.LinePrefix = OldPrefix;
            }
        }

        public override void Write(char value)
        {
            NoSeparationNeeded = false;

            // Write out indent if we are starting a new line, but only if the line isn't empty
            // (This assumes a carriage return never appears outside of a newline, which is a safe assumption for any valid files.)
            if (OnNewLine && value != '\r' && value != '\n')
            {
                OnNewLine = false;

                for (int i = 0; i < IndentLevel * IndentSize; i++)
                { Write(' '); }

                if (LinePrefix is object)
                { Write(LinePrefix); }
            }

            // Write out the actual content with the underlying writer
            CodeBuilder.Append(value);

            // If this character started a newline, update onNewLine so we know to indent the next line
            if (value == '\n')
            { OnNewLine = true; }
        }

        public void WriteIdentifier(string identifier)
            => Write(SanitizeIdentifier(identifier));

        //TODO: Handle illegal identifier characters
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
                    return identifier;
            }
        }

        public static string SanitizeStringLiteral(string value)
        {
            // Based on https://github.com/dotnet/csharplang/blob/0e365431d7ac2a6250089be9e77728ba2742d450/spec/lexical-structure.md#string-literals
            StringBuilder ret = null;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];

                // Basic replacements
                string replacement = character switch
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

        public void EnsureSeparation()
        {
            if (NoSeparationNeeded)
            { return; }

            WriteLine();
        }

        public void WriteOut(StreamWriter writer)
        {
            writer.WriteLine($"// This code was automatically generated by {Assembly.GetEntryAssembly().GetName().Name} and should not be modified by hand!");

            foreach (string usingNamespace in UsingNamespaces)
            { writer.WriteLine($"using {usingNamespace};"); }

            writer.WriteLine();

            writer.Write(CodeBuilder.ToString());
        }

        public void WriteOut(string filePath)
        {
            //TODO: We shouldn't emit the same file twice
            //TODO: This really isn't the best place for this.
            //if (File.Exists(filePath))
            //{ throw new IOException("The specified file already exists."); }
            string originalFilePathWithoutCs = filePath;
            const string csExtension = ".cs";

            if (originalFilePathWithoutCs.EndsWith(csExtension))
            { originalFilePathWithoutCs = originalFilePathWithoutCs.Substring(0, originalFilePathWithoutCs.Length - csExtension.Length); }

            int failCount = 0;
            while (File.Exists(filePath))
            {
                failCount++;
                filePath = originalFilePathWithoutCs + $"_{failCount}{csExtension}";
            }

            if (failCount > 0)
            { Console.Error.WriteLine($"WARNING: Tried to write {filePath} more than once!"); }

            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            WriteOut(writer);
        }
    }
}
