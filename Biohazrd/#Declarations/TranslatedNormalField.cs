using ClangSharp.Pathogen;
using System;

namespace Biohazrd
{
    public record TranslatedNormalField : TranslatedField
    {
        public TypeReference Type { get; init; }

        internal unsafe TranslatedNormalField(TranslationUnitParser parsingContext, TranslatedFile file, PathogenRecordField* field)
            : base(parsingContext, file, field)
        {
            if (field->Kind != PathogenRecordFieldKind.Normal)
            { throw new ArgumentException("The specified field must be a normal field.", nameof(field)); }

            Type = new ClangTypeReference(parsingContext, field->Type);
        }

        public override string ToString()
            => base.ToString();
    }
}
