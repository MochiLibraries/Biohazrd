using System;

namespace Biohazrd.Transformation.Common
{
    public record SimpleTransformation
    {
        public bool SupportsConcurrency { get; init; } = true;

        public virtual TranslatedLibrary Transform(TranslatedLibrary library)
            => new Transformation(this).Transform(library);

        public Func<TranslatedLibrary, TranslatedLibrary>? PreTransformLibrary { get; init; }
        public Func<TranslatedLibrary, TranslatedLibrary>? PostTransformLibrary { get; init; }

        public delegate TransformationResult TransformationMethod<TDeclaration>(TransformationContext context, TDeclaration declaration);
        public TransformationMethod<TranslatedDeclaration>? TransformDeclaration { get; init; }
        public TransformationMethod<TranslatedDeclaration>? TransformUnknownDeclarationType { get; init; }
        public TransformationMethod<TranslatedEnum>? TransformEnum { get; init; }
        public TransformationMethod<TranslatedEnumConstant>? TransformEnumConstant { get; init; }
        public TransformationMethod<TranslatedFunction>? TransformFunction { get; init; }
        public TransformationMethod<TranslatedParameter>? TransformParameter { get; init; }
        public TransformationMethod<TranslatedRecord>? TransformRecord { get; init; }
        public TransformationMethod<TranslatedStaticField>? TransformStaticField { get; init; }
        public TransformationMethod<TranslatedTypedef>? TransformTypedef { get; init; }
        public TransformationMethod<TranslatedUndefinedRecord>? TransformUndefinedRecord { get; init; }
        public TransformationMethod<TranslatedUnsupportedDeclaration>? TransformUnsupportedDeclaration { get; init; }
        public TransformationMethod<TranslatedVTable>? TransformVTable { get; init; }
        public TransformationMethod<TranslatedVTableEntry>? TransformVTableEntry { get; init; }
        public TransformationMethod<TranslatedField>? TransformField { get; init; }
        public TransformationMethod<TranslatedBaseField>? TransformBaseField { get; init; }
        public TransformationMethod<TranslatedNormalField>? TransformNormalField { get; init; }
        public TransformationMethod<TranslatedBitField>? TransformBitField { get; init; }
        public TransformationMethod<TranslatedUnimplementedField>? TransformUnimplementedField { get; init; }
        public TransformationMethod<TranslatedVTableField>? TransformVTableField { get; init; }

        protected class Transformation : TransformationBase
        {
            protected readonly SimpleTransformation Parent;
            protected override bool SupportsConcurrency => Parent.SupportsConcurrency;

            public Transformation(SimpleTransformation parent)
                => Parent = parent;

            protected sealed override TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
                => Parent.PreTransformLibrary is not null ? Parent.PreTransformLibrary(library) : library;

            protected sealed override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
                => Parent.PostTransformLibrary is not null ? Parent.PostTransformLibrary(library) : library;

            protected sealed override TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
                => Parent.TransformDeclaration is not null ? Parent.TransformDeclaration(context, declaration) : base.TransformDeclaration(context, declaration);
            protected sealed override TransformationResult TransformUnknownDeclarationType(TransformationContext context, TranslatedDeclaration declaration)
                => Parent.TransformUnknownDeclarationType is not null ? Parent.TransformUnknownDeclarationType(context, declaration) : base.TransformUnknownDeclarationType(context, declaration);
            protected sealed override TransformationResult TransformEnum(TransformationContext context, TranslatedEnum declaration)
                => Parent.TransformEnum is not null ? Parent.TransformEnum(context, declaration) : base.TransformEnum(context, declaration);
            protected sealed override TransformationResult TransformEnumConstant(TransformationContext context, TranslatedEnumConstant declaration)
                => Parent.TransformEnumConstant is not null ? Parent.TransformEnumConstant(context, declaration) : base.TransformEnumConstant(context, declaration);
            protected sealed override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
                => Parent.TransformFunction is not null ? Parent.TransformFunction(context, declaration) : base.TransformFunction(context, declaration);
            protected sealed override TransformationResult TransformParameter(TransformationContext context, TranslatedParameter declaration)
                => Parent.TransformParameter is not null ? Parent.TransformParameter(context, declaration) : base.TransformParameter(context, declaration);
            protected sealed override TransformationResult TransformRecord(TransformationContext context, TranslatedRecord declaration)
                => Parent.TransformRecord is not null ? Parent.TransformRecord(context, declaration) : base.TransformRecord(context, declaration);
            protected sealed override TransformationResult TransformStaticField(TransformationContext context, TranslatedStaticField declaration)
                => Parent.TransformStaticField is not null ? Parent.TransformStaticField(context, declaration) : base.TransformStaticField(context, declaration);
            protected sealed override TransformationResult TransformTypedef(TransformationContext context, TranslatedTypedef declaration)
                => Parent.TransformTypedef is not null ? Parent.TransformTypedef(context, declaration) : base.TransformTypedef(context, declaration);
            protected sealed override TransformationResult TransformUndefinedRecord(TransformationContext context, TranslatedUndefinedRecord declaration)
                => Parent.TransformUndefinedRecord is not null ? Parent.TransformUndefinedRecord(context, declaration) : base.TransformUndefinedRecord(context, declaration);
            protected sealed override TransformationResult TransformUnsupportedDeclaration(TransformationContext context, TranslatedUnsupportedDeclaration declaration)
                => Parent.TransformUnsupportedDeclaration is not null ? Parent.TransformUnsupportedDeclaration(context, declaration) : base.TransformUnsupportedDeclaration(context, declaration);
            protected sealed override TransformationResult TransformVTable(TransformationContext context, TranslatedVTable declaration)
                => Parent.TransformVTable is not null ? Parent.TransformVTable(context, declaration) : base.TransformVTable(context, declaration);
            protected sealed override TransformationResult TransformVTableEntry(TransformationContext context, TranslatedVTableEntry declaration)
                => Parent.TransformVTableEntry is not null ? Parent.TransformVTableEntry(context, declaration) : base.TransformVTableEntry(context, declaration);
            protected sealed override TransformationResult TransformField(TransformationContext context, TranslatedField declaration)
                => Parent.TransformField is not null ? Parent.TransformField(context, declaration) : base.TransformField(context, declaration);
            protected sealed override TransformationResult TransformBaseField(TransformationContext context, TranslatedBaseField declaration)
                => Parent.TransformBaseField is not null ? Parent.TransformBaseField(context, declaration) : base.TransformBaseField(context, declaration);
            protected sealed override TransformationResult TransformNormalField(TransformationContext context, TranslatedNormalField declaration)
                => Parent.TransformNormalField is not null ? Parent.TransformNormalField(context, declaration) : base.TransformNormalField(context, declaration);
            protected sealed override TransformationResult TransformBitField(TransformationContext context, TranslatedBitField declaration)
                => Parent.TransformBitField is not null ? Parent.TransformBitField(context, declaration) : base.TransformBitField(context, declaration);
            protected sealed override TransformationResult TransformUnimplementedField(TransformationContext context, TranslatedUnimplementedField declaration)
                => Parent.TransformUnimplementedField is not null ? Parent.TransformUnimplementedField(context, declaration) : base.TransformUnimplementedField(context, declaration);
            protected sealed override TransformationResult TransformVTableField(TransformationContext context, TranslatedVTableField declaration)
                => Parent.TransformVTableField is not null ? Parent.TransformVTableField(context, declaration) : base.TransformVTableField(context, declaration);

        }
    }
}
