using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Biohazrd.OutputGeneration
{
    public abstract partial class CodeWriter : TextWriter
    {
        private const int IndentSize = 4;
        private int IndentLevel = 0;
        private bool OnNewLine = true;
        private string? LinePrefix = null;
        private bool NoSeparationNeeded = true;
        private readonly StringBuilder CodeBuilder = new StringBuilder();

        public override Encoding Encoding => Encoding.Unicode;

        protected readonly OutputSession OutputSession;
        private readonly StreamWriter _Writer;
        private bool IsFinished = false;

        protected string FilePath { get; }

        protected CodeWriter(OutputSession session, string filePath)
        {
            OutputSession = session;
            FilePath = filePath;

            // We don't need this until we're marked as finished, but we open it right away to lock the file.
            _Writer = new StreamWriter(FilePath);
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

        public void WriteLineIndented(string value)
        {
            if (!OnNewLine)
            { throw new InvalidOperationException("Cannot write an indented line when the current line already contains text."); }

            using (Indent())
            { WriteLine(value); }
        }

        public void NoSeparationNeededBeforeNextLine()
            => NoSeparationNeeded = true;

        public void EnsureSeparation()
        {
            if (NoSeparationNeeded)
            { return; }

            WriteLine();
            NoSeparationNeeded = true;
        }

        public override void Write(char value)
        {
            if (IsFinished)
            { throw new InvalidOperationException("Can't write to a code writer after it has been finished."); }

            NoSeparationNeeded = false;

            // Write out indent if we are starting a new line, but only if the line isn't empty
            // (This assumes a carriage return never appears outside of a newline, which is a safe assumption for any valid files.)
            if (OnNewLine && value != '\r' && value != '\n')
            {
                OnNewLine = false;

                for (int i = 0; i < IndentLevel * IndentSize; i++)
                { Write(' '); }

                if (LinePrefix is not null)
                { Write(LinePrefix); }
            }

            // Write out the actual content with the underlying writer
            CodeBuilder.Append(value);

            // If this character started a newline, update onNewLine so we know to indent the next line
            if (value == '\n')
            { OnNewLine = true; }
        }

        public void Finish()
        {
            if (IsFinished)
            { throw new InvalidOperationException("Can't finish a code writer more than once."); }

            if (IndentLevel > 0)
            { throw new InvalidOperationException("All indent scopes should be closed before finishing."); }

            WriteOut(_Writer);
            _Writer.Flush();
            CodeBuilder.Clear();
            IsFinished = true;
        }

        protected virtual void WriteOut(StreamWriter writer)
        {
            WriteOutHeaderComment(writer);
            WriteBetweenHeaderAndCode(writer);
            writer.Write(CodeBuilder.ToString());
        }

        protected abstract void WriteOutHeaderComment(StreamWriter writer);

        protected virtual void WriteBetweenHeaderAndCode(StreamWriter writer)
        { }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!IsFinished)
                { Finish(); }

                _Writer.Dispose();
            }
            else
            {
                // Letting a CodeWriter finalize is very bad so we scream if it happens.
                // (In theory we could open a new StreamWriter and write out using that, but that'd be a pretty heavy finalizer.)
                if (Debugger.IsAttached)
                { Debugger.Break(); }
            }

            base.Dispose(disposing);
        }
    }
}
