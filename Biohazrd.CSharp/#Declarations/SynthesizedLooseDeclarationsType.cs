using System.Collections.Generic;
using System.Collections.Immutable;

namespace Biohazrd.CSharp
{
    public sealed record SynthesizedLooseDeclarationsType : TranslatedDeclaration
    {
        public ImmutableList<TranslatedDeclaration> Members { get; init; } = ImmutableList<TranslatedDeclaration>.Empty;

        public SynthesizedLooseDeclarationsType(TranslatedFile file)
            : base(file)
        { }

        public override IEnumerator<TranslatedDeclaration> GetEnumerator()
            => Members.GetEnumerator();

        public override string ToString()
            => $"Synthesized Type {base.ToString()}";
    }
}
