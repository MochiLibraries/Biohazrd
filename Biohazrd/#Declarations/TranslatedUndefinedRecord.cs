using ClangSharp;
using System;
using System.Diagnostics;

namespace Biohazrd
{
    public sealed record TranslatedUndefinedRecord : TranslatedDeclaration
    {
        internal TranslatedUndefinedRecord(TranslatedFile file, RecordDecl record)
            : base(file, record)
        {
            if (record.Definition is not null)
            { throw new ArgumentException("The specified record is defined.", nameof(record)); }

            Debug.Assert(!String.IsNullOrEmpty(record.Name), "Undefined records are expected to always have a name.");

            Accessibility = AccessModifier.Public;
        }

        public override string ToString()
            => $"Undefined Record {base.ToString()}";
    }
}
