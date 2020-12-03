`DeclarationIdTranslatedTypeReference`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd/#TypeReferences/DeclarationIdTranslatedTypeReference.cs)\]</small>

This type reference is a [`TranslatedTypeReference`](TranslatedTypeReference.md) variant which resolves to a specific `TranslatedDeclaration` based on its associated `DeclarationId` (which is a opaque unique ID assigned to every declaration created by Biohazrd.) (It is generally used to resolve declarations which were synthesized during a transformation.)

You should generally not need to worry about using this type directly. It should generally be considered an implementation detail.

This type reference performs caching internally. You do not need to worry about calling `TryResolve` on it multiple times for the same `TranslatedLibrary`.
