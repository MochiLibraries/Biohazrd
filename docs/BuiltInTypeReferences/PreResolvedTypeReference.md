`PreResolvedTypeReference`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd/#TypeReferences/PreResolvedTypeReference.cs)\]</small>

> ⚠ **Type references of this type should never be stored as a member of any declaration within a `TranslatedLibrary`.**

This type reference is a [`TranslatedTypeReference`](TranslatedTypeReference.md) variant which resolves to a specific `TranslatedDeclaration`, literally.

This type reference will only resolve for the specific `TranslatedLibrary` instance for which it were created. This type reference is intended to simplify scenarios where you're going to consume the type reference immediately and then discard it. (Such as to reuse existing type formatting infrastructure in a context where you only have the declaration.)

This type is used to optimize certain situations during output generation. If you aren't sure if this type is appropriate for your use, use [`TranslatedTypeReference.Create`](TranslatedTypeReference.md) instead.
