using ClangSharp.Pathogen;
using System;
using System.Diagnostics;

namespace Biohazrd
{
    public sealed record TranslatedVTableField : TranslatedField
    {
        private const string DefaultName = "VirtualMethodTablePointer";

        internal unsafe TranslatedVTableField(TranslationUnitParser parsingContext, TranslatedFile file, PathogenRecordField* field)
            : base(parsingContext, file, field)
        {
            if (field->Kind != PathogenRecordFieldKind.VTablePtr)
            { throw new ArgumentException("The specified field must be a virtual method table field.", nameof(field)); }

            Debug.Assert(Offset == 0, "VTable pointers are expected to be at offset 0.");

            Name = DefaultName;
        }

        /// <summary>Creates a virtual method table pointer field aliased from a base field.</summary>
        public TranslatedVTableField(TranslatedBaseField baseField)
            : base(baseField.File)
        {
            // The base must be at offset 0
            if (baseField.Offset != 0)
            { throw new ArgumentException("VTable pointer aliases must correspond to bases at offset 0."); }

            Offset = 0;
            Name = DefaultName;
        }
    }
}
