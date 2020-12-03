Terminology
===================================================================================================

The following terminology is used throughout Biohazrd and this documentation.

<!-- TOC -->

- [Generator author](#generator-author)
- [Interop library](#interop-library)
- [Unsafe interop layer](#unsafe-interop-layer)
- [Safe interop layer](#safe-interop-layer)
- [Declaration tree](#declaration-tree)
- [Translation](#translation)

<!-- /TOC -->

## Generator author
The developer who interacts with Biohazrd to produce the generator portion of an interop library.

## Interop library
A collection of C# code which handles accessing a native C/C++ library from C#.

## Unsafe interop layer
The lowest level of interop which closely matches the native C/C++ API. As the name implies, accessing this portion of the API usually requires the use of unsafe code in C#.

## Safe interop layer
An abstraction over the unsafe layer to make it easier for C# developers to consume.

## Declaration tree
An immutable tree of declarations that describes the native code being processed, the root of the tree is the `TranslatedLibrary`.

## Translation
Code which modifies the declaration tree.