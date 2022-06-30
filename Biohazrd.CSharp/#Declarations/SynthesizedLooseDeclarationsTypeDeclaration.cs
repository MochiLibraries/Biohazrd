using Biohazrd.Infrastructure;
using Biohazrd.Transformation;
using Biohazrd.Transformation.Infrastructure;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Biohazrd.CSharp
{
    public sealed record SynthesizedLooseDeclarationsTypeDeclaration : TranslatedDeclaration, ICustomTranslatedDeclaration
    {
        [CatchAllMembersProperty]
        public ImmutableList<TranslatedDeclaration> Members { get; init; } = ImmutableList<TranslatedDeclaration>.Empty;

        public SynthesizedLooseDeclarationsTypeDeclaration(TranslatedFile file)
            : base(file)
            => Accessibility = AccessModifier.Public;

        public override IEnumerator<TranslatedDeclaration> GetEnumerator()
            => Members.GetEnumerator();

        public override string ToString()
            => $"Synthesized Type {base.ToString()}";

        TransformationResult ICustomTranslatedDeclaration.TransformChildren(ITransformation transformation, TransformationContext context)
        {
            // Transform members
            ListTransformHelper newMembers = new(Members);
            foreach (TranslatedDeclaration member in Members)
            { newMembers.Add(transformation.TransformRecursively(context, member)); }

            // If this type changes, mutate it
            if (newMembers.WasChanged)
            {
                return this with
                {
                    Members = newMembers.ToImmutable()
                };
            }
            else
            { return this; }
        }

        TransformationResult ICustomTranslatedDeclaration.TransformTypeChildren(ITypeTransformation transformation, TransformationContext context)
            => this;
    }
}
