using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Biohazrd.Transformation
{
    public struct TypeTransformationContext
    {
        private readonly TransformationContext TransformationContext;

        public TranslatedLibrary Library => TransformationContext.Library;
        public ImmutableArray<TranslatedDeclaration> ParentDeclarations => TransformationContext.Parents;
        public TranslatedDeclaration? ParentDeclaration => TransformationContext.ParentDeclaration;
        public IEnumerable<TranslatedDeclaration> ParentDeclarationSiblings => TransformationContext.Parent;

        public ImmutableArray<TypeReference> Parents { get; init; }
        public TypeReference? Parent => Parents.IsEmpty ? null : Parents[Parents.Length - 1];

        public TypeTransformationContext(TransformationContext transformationContext)
        {
            TransformationContext = transformationContext;
            Parents = ImmutableArray<TypeReference>.Empty;
        }

        public static implicit operator TypeTransformationContext(TransformationContext transformationContext)
            => new TypeTransformationContext(transformationContext);

        private TypeTransformationContext(TypeTransformationContext other)
            => this = other;

        public TypeTransformationContext Add(TypeReference newParent)
            => new TypeTransformationContext(this)
            {
                Parents = Parents.Add(newParent)
            };

        public override string ToString()
        {
            StringBuilder builder = new();
            TransformationContext.ToString(builder);

            foreach (TypeReference parent in Parents)
            { builder.Append($" -> {parent}"); }

            return builder.ToString();
        }
    }
}
