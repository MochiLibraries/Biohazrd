using System.Collections.Immutable;
using System.Text;

namespace Biohazrd.Transformation
{
    public struct TransformationContext
    {
        public TranslatedLibrary Library { get; }
        public ImmutableArray<TranslatedDeclaration> Parents { get; init; }
        public TranslatedDeclaration? Parent => Parents.IsEmpty ? null : Parents[Parents.Length - 1];

        internal TransformationContext(TranslatedLibrary library)
        {
            Library = library;
            Parents = ImmutableArray<TranslatedDeclaration>.Empty;
        }

        public TransformationContext Add(TranslatedDeclaration newParent)
            => new TransformationContext(Library)
            {
                Parents = Parents.Add(newParent)
            };

        public override string ToString()
        {
            StringBuilder ret = new();
            ret.Append(nameof(TranslatedLibrary));

            foreach (TranslatedDeclaration parent in Parents)
            { ret.Append($".{parent.Name}"); }

            return ret.ToString();
        }
    }
}
