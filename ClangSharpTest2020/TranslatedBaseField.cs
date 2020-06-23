using System;
using System.Linq;

namespace ClangSharpTest2020
{
    public sealed class TranslatedBaseField : TranslatedField
    {
        public override string DefaultName { get; } = "Base";

        internal unsafe TranslatedBaseField(TranslatedRecord record, PathogenRecordField* field)
            : base(record, field)
        {
            if (field->Kind != PathogenRecordFieldKind.NonVirtualBase)
            { throw new ArgumentException("The specified field must be a non-virtual base field.", nameof(field)); }

            // We do not expect more than one base field
            if (Record.Members.Any(m => m is TranslatedBaseField && m != this))
            {
                DefaultName = Record.GetNameForUnnamed(field->Kind.ToString());
                File.Diagnostic(Severity.Warning, Context, $"Record layout contains more than one non-virtual base field, renamed redundant base to {TranslatedName}.");
            }
        }
    }
}
