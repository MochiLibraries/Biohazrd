Built-in Type References
===================================================================================================

Biohazrd provides many built-in type references, many of which are used by the [built-in transformations](../BuiltinTransformations/Readme.md).

* [`TypeReference`](TypeReference.md)
  * [`ClangTypeReference`](ClangTypeReference.md)
  * [`FunctionPointerTypeReference`](FunctionPointerTypeReference.md)
  * [`PointerTypeReference`](PointerTypeReference.md)
  * [`TranslatedTypeReference`](TranslatedTypeReference.md)
    * [`CachingTranslatedTypeReference`](CachingTranslatedTypeReference.md)
        * [`ClangDeclTranslatedTypeReference`](ClangDeclTranslatedTypeReference.md)
        * [`DeclarationIdTranslatedTypeReference`](DeclarationIdTranslatedTypeReference.md)
    * [`PreResolvedTypeReference`](PreResolvedTypeReference.md)
  * [`VoidTypeReference`](VoidTypeReference.md)
  * **C#-specific type references**
  * [`CSharpBuiltinTypeReference`](CSharpBuiltinTypeReference.md)

Generally speaking, all type references have value equality and will print something sensible for their `ToString`.
