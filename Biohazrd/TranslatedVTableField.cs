using ClangSharp.Pathogen;
using System;
using System.Diagnostics;
using System.Linq;

namespace Biohazrd
{
    public sealed class TranslatedVTableField : TranslatedField
    {
        public override string DefaultName { get; } = "VirtualMethodTablePointer";
        public string TranslatedTypeName => "VirtualMethodTable";

        internal unsafe TranslatedVTableField(TranslatedRecord record, PathogenRecordField* field)
            : base(record, field)
        {
            Debug.Assert(Offset == 0, "VTable pointers are expected to be at offset 0.");

            if (field->Kind != PathogenRecordFieldKind.VTablePtr)
            { throw new ArgumentException("The specified field must be a virtual method table field.", nameof(field)); }

            // We do not support more than one VTable field
            if (Record.Members.Any(m => m is TranslatedVTableField && m != this))
            {
                DefaultName = Record.GetNameForUnnamed(field->Kind.ToString());
                File.Diagnostic(Severity.Warning, Context, $"Record layout contains more than one non-virtual base field, renamed redundant base to {DefaultName}.");
            }
        }

        internal TranslatedVTableField(TranslatedRecord record, TranslatedBaseField baseField)
            : base(record, declaration: null, 0, record.Record.Handle, "AliasedVirtualMethodPointer", fieldType: null)
        {
            // This constructor should only be used when the record does not have its own VTable field
            if (Record.Members.Any(m => m is TranslatedVTable && m != this))
            { throw new InvalidOperationException("Base vTable aliases should not be added to records which already have a vTable pointer."); }

            // The base must be at offset 0
            if (baseField.Offset != 0)
            { throw new ArgumentException("VTable pointer aliases must correspond to bases at offset 0."); }

            //TODO: Ensure base has a vtable pointer @ 0
        }

        protected override void TranslateType(CodeWriter writer)
            => writer.Write($"{CodeWriter.SanitizeIdentifier(TranslatedTypeName)}*");
    }
}
