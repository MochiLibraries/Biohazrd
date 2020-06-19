using System;
using System.Linq;

namespace ClangSharpTest2020
{
    public sealed class TranslatedVTableField : TranslatedField
    {
        public override string TranslatedName { get; } = "VirtualMethodTablePointer";
        public string TranslatedTypeName => "VirtualMethodTable";

        internal unsafe TranslatedVTableField(TranslatedRecord record, PathogenRecordField* field)
            : base(record, field)
        {
            if (field->Kind != PathogenRecordFieldKind.VTablePtr)
            { throw new ArgumentException("The specified field must be a virtual method table field.", nameof(field)); }

            // We do not support more than one VTable field
            if (Record.Members.Any(m => m is TranslatedVTableField && m != this))
            {
                TranslatedName = Record.GetNameForUnnamed(field->Kind.ToString());
                File.Diagnostic(Severity.Warning, Context, $"Record layout contains more than one non-virtual base field, renamed redundant base to {TranslatedName}.");
            }
        }

        protected override void TranslateType(CodeWriter writer)
            => writer.Write($"{CodeWriter.SanitizeIdentifier(TranslatedTypeName)}*");
    }
}
