`CachingTranslatedTypeReference`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd/#TypeReferences/CachingTranslatedTypeReference.cs)\]</small>

This is the base type reference for [`TranslatedTypeReference`](TranslatedTypeReference.md) implementations which benefit from caching.

You should generally not need to worry about using this type directly. It should generally be considered an implementation detail.

If you find the need to implement your own [`TranslatedTypeReference`](TranslatedTypeReference.md) variant, you may find it useful to use this as your base type if your variant would benefit from caching. Simply override both `TryResolveImplementation` methods with your resolution logic.
