using ClangSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace ClangSharpTest2020
{
    public sealed class TranslatedRecord
    {
        public TranslatedLibrary Library { get; }

        public RecordDecl Record { get; }

        internal TranslatedRecord(TranslatedLibrary library, RecordDecl record)
        {
            if (!record.Handle.IsDefinition)
            { throw new ArgumentException("Only defining records can be translated!"); }

            Library = library;
            Record = record;

            Library.AddRecord(this);
        }

        public override string ToString()
            => Record.Name;
    }
}
