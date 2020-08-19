using ClangSharp.Pathogen;

namespace Biohazrd
{
    public sealed class TranslatedUnimplementedField : TranslatedField
    {
        internal unsafe TranslatedUnimplementedField(TranslatedRecord record, PathogenRecordField* field)
            : base(record, field)
            => File.Diagnostic(Severity.Warning, Context, $"{field->Kind} fields may not be translated correctly.");
    }
}
