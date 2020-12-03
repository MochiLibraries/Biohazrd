`ClangTypeReference`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd/#TypeReferences/ClangTypeReference.cs)\]</small>

(Not to be confused with [`ClangDeclTranslatedTypeReference`](ClangDeclTranslatedTypeReference.md))

This type reference represents a raw type reference as it is represented in Clang. Almost all type references emitted by the translation stage of Biohazrd will be this type reference. These references are generally eliminated by [type reduction](../BuiltInTransformations/TypeReductionTransformation.md).

Ideally you should not attempt to handle these type references unless you absolutely have to (such as to add support for a type reference Biohazrd doesn't support.) They're relatively unfriendly and leak details of how Clang understands your input C/C++ header files.
