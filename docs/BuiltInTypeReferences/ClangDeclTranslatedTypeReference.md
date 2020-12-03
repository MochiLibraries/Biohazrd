`ClangDeclTranslatedTypeReference`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd/#TypeReferences/ClangDeclTranslatedTypeReference.cs)\]</small>

(Not to be confused with [`ClangTypeReference`](ClangTypeReference.md))

This type reference is a [`TranslatedTypeReference`](TranslatedTypeReference.md) variant which resolves to a specific `TranslatedDeclaration` based on its associated Clang `Decl`s. (It is used to resolve declarations which originally came from the translation stage.)

You should generally not need to worry about using this type directly. It should generally be considered an implementation detail.

This type reference performs caching internally. You do not need to worry about calling `TryResolve` on it multiple times for the same `TranslatedLibrary`.
