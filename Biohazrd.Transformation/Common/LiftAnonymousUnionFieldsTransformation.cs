using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace Biohazrd.Transformation.Common
{
    public sealed class LiftAnonymousUnionFieldsTransformation : TransformationBase
    {
        // The value of this dictionary is not used, it's basically used as a concurrtent HashSet<TranslatedRecord>
        // union => doesnt_matter
        private readonly ConcurrentDictionary<TranslatedRecord, nint> AbsorbedUnions = new(ReferenceEqualityComparer.Instance);
        private readonly RemoveLiftedAnonymousUnionsTransformation RemoveTransformation;

        public LiftAnonymousUnionFieldsTransformation()
            => RemoveTransformation = new RemoveLiftedAnonymousUnionsTransformation(this);

        protected override TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
        {
            AbsorbedUnions.Clear();
            return library;
        }

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
        {
            TranslatedLibrary ret = RemoveTransformation.Transform(library);
            Debug.Assert(AbsorbedUnions.Count == 0, "There should not be any unremoved absorbed unions at this point.");
            return ret;
        }

        protected override TransformationResult TransformNormalField(TransformationContext context, TranslatedNormalField declaration)
        {
            // If the field is unnamed and is typed by a TranslatedTypeReference that resolves to a union...
            if (declaration.IsUnnamed
                && declaration.Type is TranslatedTypeReference reference
                && reference.TryResolve(context.Library) is TranslatedRecord { Kind: RecordKind.Union, IsUnnamed: true } union)
            {
                // Replace this field with the members of the union
                TransformationResult result = new();
                foreach (TranslatedDeclaration unionMember in union)
                {
                    // Ensure the logic of this switch statement is duplicated above in TransformRecord
                    switch (unionMember)
                    {
                        // Offset fields to the location of the field we're replacing
                        case TranslatedNormalField field:
                            result.Add(field with { Offset = field.Offset + declaration.Offset });
                            break;
                        // Anonymous records can occur in an anonymous union, just pass them through.
                        case TranslatedRecord { IsUnnamed: true } record:
                            result.Add(record);
                            break;
                        // Anything else is unexpected and aborts the transformation
                        default:
                            return declaration;
                    }
                }

                // Mark the union to be removed
                if (!AbsorbedUnions.TryAdd(union, 0))
                { Debug.Assert(false, "An anonymous union would not be used by more than once field."); }

                return result;
            }

            return declaration;
        }

        private sealed class RemoveLiftedAnonymousUnionsTransformation : TransformationBase
        {
            private readonly LiftAnonymousUnionFieldsTransformation Parent;

            public RemoveLiftedAnonymousUnionsTransformation(LiftAnonymousUnionFieldsTransformation parent)
                => Parent = parent;

            protected override TransformationResult TransformRecord(TransformationContext context, TranslatedRecord declaration)
            {
                // If this record was a union which was absorbed, remove it
                if (Parent.AbsorbedUnions.TryRemove(declaration, out _))
                { return null; }

                return declaration;
            }
        }
    }
}
