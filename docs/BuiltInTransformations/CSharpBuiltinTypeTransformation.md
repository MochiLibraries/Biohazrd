`CSharpBuiltinTypeTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.CSharp/#Transformations/CSharpBuiltinTypeTransformation.cs)\]</small>

This transformation is similar to [`CSharpTypeReductionTransformation`](CSharpTypeReductionTransformation.md). It specifically handles things like converting `unsigned int` to `uint`.

More specifically, it handles reducing [`ClangTypeReference`](../BuiltInTypeReferences/ClangTypeReference.md)s for C/C++ built-in types to [`CSharpBuiltinTypeReference`](../BuiltInTypeReferences/CSharpBuiltinTypeReference.md) types.

## When this transformation is applicable

This transformation should almost always be used when you're using Biohazrd to generate a C# interop library.

## Interacting with this transformation

This transformation has the same interaction cocerns as [`TypeReductionTransformation`](TypeReductionTransformation.md), see that article for details.

This transformation should be run after type reduction. (IE: [`CSharpTypeReductionTransformation`](CSharpTypeReductionTransformation.md))
