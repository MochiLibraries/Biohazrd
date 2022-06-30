using ClangSharp;

namespace Biohazrd;

public sealed record TranslatedRecordTemplate : TranslatedTemplate
{
    public RecordKind Kind { get; init; }

    public TranslatedRecordTemplate(TranslatedFile file, ClassTemplateDecl recordTemplate)
        : base(file, recordTemplate)
    {
        //TODO: In theory we could maybe represent this as a TranslatedRecord child of this type
        // Problem is this record will not have layout info, which doesn't jive well with the TranslatedRecord/TranslatedField.
        CXXRecordDecl record = recordTemplate.TemplatedDecl;

        if (record.IsStruct)
        { Kind = RecordKind.Struct; }
        else if (record.IsUnion)
        { Kind = RecordKind.Union; }
        else if (record.IsClass)
        { Kind = RecordKind.Class; }
        else
        { Kind = RecordKind.Unknown; }
    }

    public override string ToString()
        => $"Record{base.ToString()}";
}
