`OrganizeOutputFilesByNamespaceTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.CSharp/#Transformations/OrganizeOutputFilesByNamespaceTransformation.cs)\]</small>

This transformation moves the output files into folders based on their namespace as per the standard C# convention.

## When this transformation is applicable

This transformation is totally optional, but is generally desirable when the output uses multi-level namespaces. It should be applied after any transformations which affect namespaces.

## Details

Files are moved into folders based on a given root namespace by manipulating the declaration's associated `OutputFileName` metadata. If a declaration is in the global namespace, it is not moved into any folder.

If the declaration is not within the root namespace, it will be written as if it were. (IE: If the root namespace is `MyLibrary` and a declaration is `Other::SomeNamespace::SomeType` then it will be written to `<Output Root>/Other/SomeNamespace/SomeType.cs`.
