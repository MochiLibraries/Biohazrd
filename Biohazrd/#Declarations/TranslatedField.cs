using ClangSharp;
using ClangSharp.Pathogen;

namespace Biohazrd
{
    public abstract record TranslatedField : TranslatedDeclaration
    {
        public long Offset { get; init; }

        /// <summary>Constructs a field for a synthesized field.</summary>
        protected TranslatedField(TranslatedFile file)
            : base(file)
        { }

        /// <summary>Constructs a field which replaces another declaration.</summary>
        protected TranslatedField(TranslatedDeclaration replacedDeclaration)
            : base(replacedDeclaration)
        { }

        private protected unsafe TranslatedField(TranslationUnitParser parsingContext, TranslatedFile file, PathogenRecordField* field)
            : base(file, TryGetDecl(parsingContext, field))
        {
            Offset = field->Offset;
            Name = field->Name.ToString();
        }

        private static unsafe Decl? TryGetDecl(TranslationUnitParser parsingContext, PathogenRecordField* field)
        {
            if (field->Kind != PathogenRecordFieldKind.Normal || field->FieldDeclaration.IsNull)
            { return null; }

            return (Decl)parsingContext.FindCursor(field->FieldDeclaration);
        }

        internal static unsafe TranslatedField Create(TranslationUnitParser parsingContext, TranslatedFile file, PathogenRecordField* field)
            => field->Kind switch
            {
                PathogenRecordFieldKind.Normal
                    => field->IsBitField == 0 ? new TranslatedNormalField(parsingContext, file, field) : new TranslatedBitField(parsingContext, file, field),
                PathogenRecordFieldKind.NonVirtualBase => new TranslatedBaseField(parsingContext, file, field),
                PathogenRecordFieldKind.VTablePtr => new TranslatedVTableField(parsingContext, file, field),
                _ => new TranslatedUnimplementedField(parsingContext, file, field)
            };

        public override string ToString()
            => $"Field {base.ToString()} @ {Offset}";
    }
}
