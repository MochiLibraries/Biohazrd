`ResolveTypedefsTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.CSharp/#Transformations/ResolveTypedefsTransformation.cs)\]</small>

This transformation replaces all references that resolve to a `TranslatedTypedef` declaration with the typedef's underlying type.

## When this transformation is applicable

This transformation is completely optional.

This transformation is most useful if you want to be able to avoid reasoning about any `TranslatedTypeReference`s which resolve to `TranslatedTypedef`. In other words, this transformation makes it easier to write other transformations.

For example, if you want to match all fields which have use `uint` it'll be easier to check for them if this transformation has been applied.

## Interacting with this transformation

As noted above, this transformation is completely optional.

This transformation should be run as late as possible, but before any transformations which rely on the simplifications it provides. Since this transformation destroys typedef information, it must be run before any transformations which care about it.

## Example

Given the following C++ example:

```cpp
typedef int MyInteger;

MyInteger GetFavoriteNumber();
```

Biohazrd's translation stage will output the following declaration tree:

```
TranslatedTypedef MyInteger -> `int`
TranslatedFunction GetFavoriteNumber -> `MyInteger`
```

After applying this transformation, the declaration tree bcomes the following:

```
TranslatedFunction GetFavoriteNumber -> `int`
```

Note that the typedef was removed as well. This is done to prevent confusion since the typedef is no longer relevant to the output.

This transformation has no impact on the output. (If you had not run the transformation, Biohazrd would've resolved the typedef at emit time.)
