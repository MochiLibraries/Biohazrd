`StripUnreferencedLazyDeclarationsTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.Transformation/Common/StripUnreferencedLazyDeclarationsTransformation.cs)\]</small>

This transformation implements the funcionality specified by the `LazilyGenerated` metadata item. In short, this transformation will remove any unreferenced record declarations marked with `LazilyGenerated`.

Anonymous structs, classes, and unions from C++ are always marked with `LazilyGenerated`. You can use `LazilyGenerated` in conjunction with this transformation to remove any uneeded types from your library. For example, you might mark all declarations from a specific internal header file with `LazilyGenerated` to avoid them appearing in your output unecessarily.

## When this transformation is applicable

This transformation should generally always be used, and it must be used if you rely on the behavior described by `LazilyGenerated`.

## Interacting with this transformation

This transformation must be run after `TypeReductionTransformation` or an equivalent. It does not handle type references which have not been reduced, so failing to do so will result in it removing all lazily-generated records.
