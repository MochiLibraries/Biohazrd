`DeduplicateNamesTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.Transformation/Common/DeduplicateNamesTransformation.cs)\]</small>

This transformation looks for any declarations with conflicting names and adds a suffix to disambiguate them, which ensures that the resulting C# code does not contain any [CS0102 name conflicts](https://docs.microsoft.com/en-us/dotnet/csharp/misc/cs0102).

This is especially useful when parameter names are missing, which is permitted in C/C++ but not in C#.

This transformation intentionally does not affect functions because it is assumed they are disambiguated by function overloading. ([`ConstOverloadRenameTransformation`](ConstOverloadRenameTransformation.md) handles the case of disambiguating `const` overloads since they don't translate to C#.)

## When this transformation is applicable

This transformation should generally always be used. Ideally it does nothing, but it ensures your resulting interop library still builds.

## Interacting with this transformation

This transformation causes `IsUnnamed` to be cleared on any declarations it affects. As such, if you write a transformation which relies on `IsUnnamed` it should run before this one.

This transformation should usually be the last transformation you run before verification. This ensures it runs with the final view of your translated library.

## Details

As noted in the summary, this transformation most frequently applies in the case of unnamed parameters:

```cpp
void SomeFunction(int);
void SomeFunction(int, int);
```

For this C++, Biohazrd's translation stage will output the following declaration tree:

```
TranslatedFunction SomeFunction
    TranslatedParameter <>UnnamedTranslatedParameter (IsUnnamed)
TranslatedFunction SomeFunction
    TranslatedParameter <>UnnamedTranslatedParameter (IsUnnamed) ☣
    TranslatedParameter <>UnnamedTranslatedParameter (IsUnnamed) ☣
```

As you can see, the second `SomeFunction` has two parameters with the same placeholder name since the C++ header did not give the parameters names.

This transformation will rename the two parameters marked with ☣:

```
TranslatedFunction SomeFunction
    TranslatedParameter <>UnnamedTranslatedParameter (IsUnnamed)
TranslatedFunction SomeFunction
    TranslatedParameter <>UnnamedTranslatedParameter_0 ☣
    TranslatedParameter <>UnnamedTranslatedParameter_1 ☣
```

Note that this transformation causes `IsUnnamed` to be `false` on these parameters since this transformation is technically explicitly naming these parameters. If you rely on `IsUnnamed` in your transformations, make sure they run before this one.

Also note that neither function was affected as they're disambiguated by being overloads of eachother.
