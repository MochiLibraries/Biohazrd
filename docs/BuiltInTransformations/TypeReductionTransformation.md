`TypeReductionTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.Transformation/Common/TypeReductionTransformation.cs)\]</small>

> ℹ You should generally not use this transformation directly, see [`CSharpTypeReductionTransformation`](CSharpTypeReductionTransformation.md)

When Biohazrd finishes translating your input C/C++ header files into the `TranslatedDeclaration` tree, the entire tree only contains raw, unfriendly [`ClangTypeReference`](../BuiltInTypeReferences/ClangTypeReference.md) type references. The process of transforming these Clang type references into more friendly Biohazrd ones is called "type reduction".

For example, `void` as a [`ClangTypeReference`](../BuiltInTypeReferences/ClangTypeReference.md) is represented as a `ClangSharp.BuiltinType` with the `Kind` set to `CXTypeKind.CXType_Void`. Clang type references also include information like whether the type was namespace qualified, whether they used the C++ `auto` keyword, etc.

This transformation is persistent, so it will keep running on [`ClangTypeReference`](../BuiltInTypeReferences/ClangTypeReference.md)s until they're all gone.

## When this transformation is applicable

This transformation should generally always be used via [`CSharpTypeReductionTransformation`](CSharpTypeReductionTransformation.md).

If you need to modify the behavior of this reduction transformation, you should implement your own transformation which extends either it or [`CSharpTypeReductionTransformation`](CSharpTypeReductionTransformation.md).

## Interacting with this transformation

How you implement your own transformations is heavily affected by whether your transformation will run before or after this one.

If you need to work with type references, you almost always want your transformation to run after type reduction.

If you intend to work with Clang types directly (perhaps to handle [a type class not yet handled by Biohazrd](https://github.com/InfectedLibraries/Biohazrd/issues/38) or you want to handle it differently) then you likely want to either extend this transformation or run before it does. (If the type class you're planning to work with has nested type references, you almost certainly want to extend this transformation to ensure persistent type reduction is handled appropriately.)

If you're extending this type to add support for a type class Biohazrd doesn't support, please consider commenting on [the Clang type classes meta issue](https://github.com/InfectedLibraries/Biohazrd/issues/38). Most of the unsupported type classes are unsupported due to a lack of real-world examples.

## Type reductions handled by this transformation

> ℹ Moreso than other transformations, details of this implementation are subject to change as improvements are made to Biohazrd.

* `void` is converted to [`VoidTypeReference`](../BuiltInTypeReferences/VoidTypeReference.md).
* Elaborated namespaces are stopped. (IE: `physx::PxU32` is changed to `PxU32`.)
* `auto` and `decltype` are converted to their inferred types.
* For `typedef` types:
  * If their `typedef` declaration resolves within the `TranslatedLibrary` they are converted to a [`TranslatedTypeReference`](../BuiltInTypeReferences/TranslatedTypeReference.md) pointing to that declaration.
    * We check if it resolves first the `typedef` might have been [deleted](RemoveRemainingTypedefsTransformation.md).
    * We don't warn if it resolves to a `TranslatedTypedef` in case it is replaced with something else later on. (It may be desireable to do `typedef` replacement after type reduction when you need to work with multiple types.)
  * Otherwise, the type is replaced with its underlying type.
    * This is done because it is assumed that you removed the `typedef` since it will not be meaningful in your interop library.
* Pointers are reduced to [`PointerTypeReference`](../BuiltInTypeReferences/PointerTypeReference.md).
* References (IE: `int&`) are reduced to [`PointerTypeReference`](../BuiltInTypeReferences/PointerTypeReference.md) with `WasReference` set to `true`.
* Constant-sized arrays (IE: `int x[10]`) in the context of function parameters are reduced to pointers.
  * Note that [`CSharpTypeReductionTransformation`](CSharpTypeReductionTransformation.md) overrides this behavior.
* Incomplete arrays (IE: `int x[]`) in the context of function parameters are reduced to pointers.
* Function pointers are converted to [`FunctionPointerTypeReference`](../BuiltInTypeReferences/FunctionPointerTypeReference.md).
* Enum type references are converted to a [`TranslatedTypeReference`](../BuiltInTypeReferences/TranslatedTypeReference.md) which resolves to the referenced enum.
* Record (`class`/`struct`) type references are converted to a [`TranslatedTypeReference`](../BuiltInTypeReferences/TranslatedTypeReference.md) which resolves to the referenced record.
