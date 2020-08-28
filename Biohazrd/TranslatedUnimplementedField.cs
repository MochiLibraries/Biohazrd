using ClangSharp.Pathogen;

namespace Biohazrd
{
    public sealed record TranslatedUnimplementedField : TranslatedField
    {
        public PathogenRecordFieldKind Kind { get; }

        internal unsafe TranslatedUnimplementedField(TranslationUnitParser parsingContext, TranslatedFile file, PathogenRecordField* field)
            : base(parsingContext, file, field)
            => Kind = field->Kind;
    }
}
