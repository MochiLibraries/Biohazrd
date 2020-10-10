using ClangSharp.Pathogen;
using System;

namespace Biohazrd
{
    public record TranslatedBitField : TranslatedNormalField
    {
        public int BitOffset { get; init; }
        public int BitWidth { get; init; }

        internal unsafe TranslatedBitField(TranslationUnitParser parsingContext, TranslatedFile file, PathogenRecordField* field)
            : base(parsingContext, file, field)
        {
            if (field->IsBitField == 0)
            { throw new ArgumentException("The specified field must be a bit field.", nameof(field)); }

            BitOffset = checked((int)field->BitFieldStart);
            BitWidth = checked((int)field->BitFieldWidth);
        }

        public override string ToString()
            => $"{base.ToString()}[{BitOffset}..{BitOffset + BitWidth}]";
    }
}
