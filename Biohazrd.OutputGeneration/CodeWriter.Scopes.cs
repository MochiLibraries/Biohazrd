using System;

namespace Biohazrd.OutputGeneration
{
    partial class CodeWriter
    {
        public IndentScope Indent()
            => new IndentScope(this);

        protected IndentScope CreateIndentScope(string? startLine, string? endLine)
            => new IndentScope(this, startLine, endLine);

        public readonly struct IndentScope : IDisposable
        {
            private readonly CodeWriter Writer;
            private readonly int ExpectedIndentLevel;
            private readonly string? EndLine;

            internal IndentScope(CodeWriter writer, string? startLine, string? endLine)
            {
                if (writer == null)
                { throw new ArgumentNullException(nameof(writer)); }

                Writer = writer;

                if (startLine != null)
                { Writer.WriteLine(startLine); }

                Writer.IndentLevel++;
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

                Writer.IndentLevel--;

                if (EndLine != null)
                { Writer.WriteLine(EndLine); }
            }
        }

        protected LeftAdjustedScope CreateLeftAdjustedScope(string startLine, string endLine)
            => new LeftAdjustedScope(this, startLine, endLine);

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

                Writer.WriteLineLeftAdjusted(startLine);
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
            private readonly string? OldPrefix;

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
    }
}
