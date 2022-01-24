`CSharpTypeReductionTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.CSharp/#Transformations/CSharpTypeReductionTransformation.cs)\]</small>

> ℹ This transformation is derived from [`TypeReductionTransformation`](TypeReductionTransformation.md), make sure you understand it too.

This type reduction transformation builds on [`TypeReductionTransformation`](TypeReductionTransformation.md) to add C#-specific type reductions, such as reducing `unsigned long long` in C++ to `ulong` in C#.

## When this transformation is applicable

This transformation should almost always be used when you're using Biohazrd to generate a C# interop library.

See [`TypeReductionTransformation`](TypeReductionTransformation.md) for details on how to augment this transformation if you need to override or change its behavior.

## Interacting with this transformation

This transformation has the same interaction concerns as [`TypeReductionTransformation`](TypeReductionTransformation.md), see that article for details.

## Type reductions handled by this transformation

> ℹ Moreso than other transformations, details of this implementation are subject to change as improvements are made to Biohazrd.

* Constant-sized arrays (IE: `int x[10]`) in all contexts are replaced with a [`TranslatedTypeReference`](../BuiltInTypeReferences/TranslatedTypeReference.md) which refers to a synthesized [`ConstantArrayTypeDeclaration`](../BuiltInDeclarations/ConstantArrayTypeDeclaration.md) which represents the constant-sized array in C#.
