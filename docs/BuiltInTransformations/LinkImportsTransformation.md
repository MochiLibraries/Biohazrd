`LinkImportsTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.Transformation/Common/LinkImportsTransformation.cs)\]</small>

This transformation loads one or more import libraries (`.lib`) files and resolves symbols in your translated library to the appropriate DLLs.

Essentially this transformation acts like a linker. It uses the same `.lib` files the C++ linker normally uses to resolve the location of functions and data in DLLs.

## When this transformation is applicable

This transformation is primarily intended for scenarios when the native code is spread througout multiple DLLs and it's non-trivial to simply assign the same DLL file name to all symbols in the library. However it's always valid to use this transformation and it can help diagnose issues where Biohazrd translates a symbol which isn't actually exported.

Note: This transformation does not know about or support `InlineReferenceFileGenerator` yet.

## Interacting with this transformation

You can think of this transformation as the linker between your generated code library and the native C/C++ library.

To use this transformation: Create it, add one or more libraries with `AddLibrary`, and then apply the transformation.

The order you add libraries matters. If you add more than one library that have an import for a given symbol, only the first import will be used.

It is not an error to specify export (static) libraries, but Biohazrd does not support linking to static libraries. (If a symbol can only be found as an export, a diagnostic will be attached to the affected declaration.

This transformation includes some flags you can configure to change how it emits diagnostics:

* `WarnOnAmbiguousSymbols`: If `true`, a warning will be issued if a symbol is ambiguous. (Default: `true`)
    * For example, consider if you add two libraries to the transformation: `A.lib` and `B.lib`.
    * `A.lib` imports `MyFunction` from `A.dll`
    * `B.lib` imports `MyFunction` from `B.dll`
    * The `TranslatedFunction` corresponding to `MyFunction` will be imported from `A.dll` and (if this flag is true) a warning will be issued.
    * (Note: If two libraries import a symbol from the same DLL, no warning is issued.)
* `ErrorOnMissing`: If `true`, an error will be issued if any symbol cannot be resolved. (Default: `false`)
    * A warning is normally issued if a symbol only resolves to a static library export. Enabling this option changes that warning to an error.
* `TrackVerboseImportInformation`: If `true`, extra information will be gathered during library load to provide more information in diagnostics. (Default: `false`)
    * This option must be configured before any calls to `AddLibrary`.
