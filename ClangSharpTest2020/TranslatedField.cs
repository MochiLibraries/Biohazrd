using ClangSharp.Interop;
using System;
using ClangType = ClangSharp.Type;

namespace ClangSharpTest2020
{
    public abstract class TranslatedField : TranslatedDeclaration
    {
        public TranslatedRecord Record { get; }
        public override bool CanBeRoot => false;

        public long Offset { get; }
        public override string TranslatedName { get; }

        protected virtual string AccessModifier => "internal";
        
        protected ClangType FieldType { get; }
        protected CXCursor Context { get; }

        private protected unsafe TranslatedField(TranslatedRecord record, PathogenRecordField* field)
            : base(record)
        {
            Record = record;
            Offset = field->Offset;
            Context = field->Kind == PathogenRecordFieldKind.Normal ? field->FieldDeclaration : Record.Record.Handle;
            TranslatedName = field->Name.ToString();
            FieldType = File.FindType(field->Type);

            // Give unnamed fields a default name
            if (String.IsNullOrEmpty(TranslatedName))
            {
                TranslatedName = Record.GetNameForUnnamed(field->Kind.ToString());
                File.Diagnostic(Severity.Warning, Context, $"Nameless field at offset {Offset} in {Record.TranslatedName} automatically renamed to {TranslatedName}");
            }
        }

        protected virtual void TranslateType(CodeWriter writer)
            => File.WriteType(writer, FieldType, Context, TypeTranslationContext.ForField);

        public override void Translate(CodeWriter writer)
        {
            writer.Using("System.Runtime.InteropServices");

            writer.EnsureSeparation();
            writer.Write($"[FieldOffset({Offset})] {AccessModifier} ");
            TranslateType(writer);
            writer.Write(" ");
            writer.WriteIdentifier(TranslatedName);
            writer.WriteLine(";");
        }

        internal static unsafe TranslatedField Create(TranslatedRecord record, PathogenRecordField* field)
            => field->Kind switch
            {
                PathogenRecordFieldKind.Normal => new TranslatedNormalField(record, field),
                PathogenRecordFieldKind.NonVirtualBase => new TranslatedBaseField(record, field),
                PathogenRecordFieldKind.VTablePtr => new TranslatedVTableField(record, field),
                _ => new TranslatedUnimplementedField(record, field)
            };
    }
}
