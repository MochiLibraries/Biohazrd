using System;

namespace Biohazrd.Transformation.Common
{
    public partial record SimpleTransformation
    {
        public bool SupportsConcurrency { get; init; } = true;

        public virtual TranslatedLibrary Transform(TranslatedLibrary library)
            => new Transformation(this).Transform(library);

        public Func<TranslatedLibrary, TranslatedLibrary>? PreTransformLibrary { get; init; }
        public Func<TranslatedLibrary, TranslatedLibrary>? PostTransformLibrary { get; init; }

        public delegate TransformationResult TransformationMethod<TDeclaration>(TransformationContext context, TDeclaration declaration);
        public TransformationMethod<TranslatedDeclaration>? TransformDeclaration { get; init; }

        protected partial class Transformation : TransformationBase
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
        }
    }
}
