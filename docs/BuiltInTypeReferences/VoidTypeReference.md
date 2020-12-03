`VoidTypeReference`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd/#TypeReferences/VoidTypeReference.cs)\]</small>

This type reference represents `void`. It is singleton, accessed via `VoidTypeReference.Instance` or `VoidTypeReference.PointerInstance` for `void` and `void*` respectively.

> ℹ `PointerInstance` is really only provided for convienence. It's possible to manually create `void*` too.

This is a separate type for two big reasons:

* `void` tends to be a bit odd so in some ways (it's a type but not really) it makes sense to make it its own thing for easy identification.
* Biohazrd uses it during the translation stage (particularly for certain entries of vtables), which does not have access to things like [`CSharpBuiltinTypeReference`](CSharpBuiltinTypeReference.md).
  * Historically Biohazrd used a [`ClangTypeReference`](ClangTypeReference.md) to Clang's built-in `void` type, but this proved problematic in certain cases and it makes more sense to pre-reduce it anyway.

## Example

Given the following C code:

```c
void MyFunction();
void* malloc();
```

You can expect the following declaration tree after type reduction has ocurred:

```
TranslatedFunction MyFunction
    ReturnType: VoidTypeReference void ☣
TranslatedFunction malloc
    ReturnType: PointerTypeReference void*
        Inner: VoidTypeReference void ☣
```

The corresponding C# variable definitions are:

```csharp
public static extern void MyFunction();
public static extern void* malloc();
``
