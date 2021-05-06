namespace Biohazrd.Transformation.Common
{
    public sealed class LiftAnonymousRecordFieldsTransformation : TransformationBase
    {
        protected override TransformationResult TransformNormalField(TransformationContext context, TranslatedNormalField declaration)
            => TransformNormalField(context.Library, declaration);

        private TransformationResult TransformNormalField(TranslatedLibrary library, TranslatedNormalField declaration)
        {
            // If the field is unnamed and it's typed by an anonymous type, replace it with the members of the type with the appropriate offset
            if (declaration is { IsUnnamed: true, Type: TranslatedTypeReference typeReference } && typeReference.TryResolve(library) is TranslatedRecord { IsUnnamed: true } anonymousType)
            {
                TransformationResult result = new();
                TranslatedRecord? currentAnonymousRewriteTargetRecord = null; // Null indicates there is no anonymous rewrite target
                TypeReference? currentAnonymousRewriteTypeReference = null; // Null indicates the rewrite target has not been cloned yet

                foreach (TranslatedDeclaration anonymousMember in anonymousType)
                {
                    switch (anonymousMember)
                    {
                        // Anonymous records can best nested
                        // If the anonymous record is used to type one or more explicitly named fields, it will be cloned to this context for those fields.
                        // (The cloning is necessary to avoid keeping the old containing anonymous record alive since the reference will resolve to the old
                        //  child and this transformation relies on lazy generation to prevent the now-unused records from being output.)
                        // If it is used to type an unnamed field, it will not be cloned as its fields will be lifted as well.
                        //
                        // Note that this implementation relies on the fact that declarations naturally translated from Clang will always be ordered such
                        // that the anonymous record comes immediately before the field(s) which use it.
                        case TranslatedRecord { IsUnnamed: true } record:
                            currentAnonymousRewriteTargetRecord = record;
                            currentAnonymousRewriteTypeReference = null;
                            break;
                        // Offset fields relative to the field we're replacing
                        // If the field is also an anonymous field, recursively replace it too
                        case TranslatedNormalField field:
                        {
                            TypeReference type = field.Type;

                            // If necessary, clone the anonymous type and/or rewrite the type reference for anonymous type fields with explicit names
                            if (!field.IsUnnamed
                                && currentAnonymousRewriteTargetRecord is not null
                                && type is TranslatedTypeReference translatedTypeReference
                                && ReferenceEquals(translatedTypeReference.TryResolve(library), currentAnonymousRewriteTargetRecord))
                            {
                                // Clone the anonymous type if needed
                                if (currentAnonymousRewriteTypeReference is null)
                                {
                                    TranslatedRecord clone = currentAnonymousRewriteTargetRecord.CreateUniqueClone();
                                    result.Add(clone);
                                    currentAnonymousRewriteTypeReference = TranslatedTypeReference.Create(clone);
                                }

                                type = currentAnonymousRewriteTypeReference;
                            }

                            TranslatedNormalField offsettedField = field with
                            {
                                Offset = field.Offset + declaration.Offset,
                                Type = type
                            };
                            result.AddRange(TransformNormalField(library, offsettedField));
                            break;
                        }
                        // Anything else is unexpected and aborts the transformation
                        default:
                            return declaration with { Diagnostics = declaration.Diagnostics.Add(Severity.Warning, "Could not lift anonymous record fields ") };
                    }
                }
                return result;
            }

            return declaration;
        }
    }
}
