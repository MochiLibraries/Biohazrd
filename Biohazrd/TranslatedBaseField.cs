using ClangSharp.Pathogen;
using System;

namespace Biohazrd
{
    public sealed record TranslatedBaseField : TranslatedField
    {
        public TranslatedTypeReference Type { get; init; }
        public bool IsPrimary { get; init; }

        internal unsafe TranslatedBaseField(TranslationUnitParser parsingContext, TranslatedFile file, PathogenRecordField* field)
            : base(parsingContext, file, field)
        {
            if (field->Kind != PathogenRecordFieldKind.NonVirtualBase)
            { throw new ArgumentException("The specified field must be a non-virtual base field.", nameof(field)); }

            Type = new TranslatedTypeReference(parsingContext.FindType(field->Type));
            IsPrimary = field->IsPrimaryBase != 0;
            Name = "Base";
        }
    }
}
