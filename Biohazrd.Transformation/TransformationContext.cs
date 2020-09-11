using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Biohazrd.Transformation
{
    public struct TransformationContext
    {
        public TranslatedLibrary Library { get; }
        public ImmutableArray<TranslatedDeclaration> Parents { get; init; }

        /// <summary>The parent declaration of the declaration (if it has one.)</summary>
        /// <remarks>To enumerate siblings of the declaration, use <see cref="Parent"/> instead.</remarks>
        public TranslatedDeclaration? ParentDeclaration => Parents.IsEmpty ? null : Parents[Parents.Length - 1];

        /// <summary>An enumerable that represents the parent of the declaration.</summary>
        /// <remarks>
        /// By nature, this enumerable will include the declaration being transformed.
        ///
        /// To enumerate children of the parent recursively, use <c>context.Parent.EnumerateRecursively()</c>
        /// </remarks>
        public IEnumerable<TranslatedDeclaration> Parent => (IEnumerable<TranslatedDeclaration>?)ParentDeclaration ?? Library;

        internal TransformationContext(TranslatedLibrary library)
        {
            Library = library;
            Parents = ImmutableArray<TranslatedDeclaration>.Empty;
        }

        private TransformationContext(TransformationContext other)
            => this = other;

        public TransformationContext Add(TranslatedDeclaration newParent)
            => new TransformationContext(this)
            {
                Parents = Parents.Add(newParent)
            };

        public override string ToString()
        {
            StringBuilder builder = new();
            ToString(builder);
            return builder.ToString();
        }

        internal void ToString(StringBuilder builder)
        {
            builder.Append(nameof(TranslatedLibrary));

            foreach (TranslatedDeclaration parent in Parents)
            { builder.Append($".{parent.Name}"); }
        }
    }
}
