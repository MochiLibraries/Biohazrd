`AutoNameUnnamedParametersTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.Transformation/Common/AutoNameUnnamedParametersTransformation.cs)\]</small>

This transformation automatically adds names to unnamed parameters.

## When this transformation is applicable

This transformation should generally always be used unless you're manually handling unnamed parameters on your own. If your library does not contain any unnamed parameters, this transformation does nothing.

## Interacting with this transformation

This transformation relies on `IsUnnamed`. As such it should be run before any transformations which affect names in bulk. (Such as `DeduplicateNamesTransformation`)

## Details

This transformation provides a more elegant disambiguation of unnamed parameters compared to `DeduplicateNamesTransformation` (which is a last-restort transformation to ensure the library can be used.)

Consider the valid C++ function declarations:

```cpp
void SomeFunction(int, int y, int);
```

For this C++, Biohazrd's translation stage will output the following declaration tree:

```
TranslatedFunction SomeFunction
    TranslatedParameter <>UnnamedTranslatedParameter (IsUnnamed) ☣
    TranslatedParameter y
    TranslatedParameter <>UnnamedTranslatedParameter (IsUnnamed) ☣
```

This transformation will automatically name the two parameters marked with ☣:

```
TranslatedFunction SomeFunction
    TranslatedParameter arg0 ☣
    TranslatedParameter y
    TranslatedParameter arg2 ☣
```
