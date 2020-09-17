using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Biohazrd
{
    public struct VisitorContext
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

        internal bool IsDefault => Library is null;

        internal VisitorContext(TranslatedLibrary library)
        {
            Library = library;
            Parents = ImmutableArray<TranslatedDeclaration>.Empty;
        }

        private VisitorContext(VisitorContext other)
            => this = other;

        public VisitorContext Add(TranslatedDeclaration newParent)
            => new VisitorContext(this)
            {
                Parents = Parents.Add(newParent)
            };

        public VisitorContext MakePrevious()
        {
            if (Parents.Length == 0)
            { throw new InvalidOperationException("Can't create previous context from root context."); }

            return new VisitorContext(this)
            {
                Parents = Parents.RemoveAt(Parents.Length - 1)
            };
        }

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
