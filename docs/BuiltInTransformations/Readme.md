Built-in Transformations
===================================================================================================

Biohazrd provides many built-in transformations for use in your generators. These transformations handle common tasks generally needed by most interop generators. This page provides an overview of them with a description of what they do for you, as well as information about when you might (or might not) want to use them. (Note that some transformations in Biohazrd support handling C++ features and should generally not be skipped unless you decided to handle those C++ features yourself for whatever reason.)

The transformations are listed in the order they're typically applied.

* [`BrokenDeclarationExtractor`](BrokenDeclarationExtractor.md)
* [`RemoveExplicitBitFieldPaddingFieldsTransformation`](RemoveExplicitBitFieldPaddingFieldsTransformation.md)
* [`AddBaseVTableAliasTransformation`](AddBaseVTableAliasTransformation.md)
* [`ConstOverloadRenameTransformation`](ConstOverloadRenameTransformation.md)
* [`MakeEverythingPublicTransformation`](MakeEverythingPublicTransformation.md)
* [`RemoveRemainingTypedefsTransformation`](RemoveRemainingTypedefsTransformation.md)
* [`TypeReductionTransformation`](TypeReductionTransformation.md)
* [`CSharpTypeReductionTransformation`](CSharpTypeReductionTransformation.md)
* [`CSharpBuiltinTypeTransformation`](CSharpBuiltinTypeTransformation.md)
* [`LiftAnonymousUnionFieldsTransformation`](LiftAnonymousUnionFieldsTransformation.md)
* [`KludgeUnknownClangTypesIntoBuiltinTypesTransformation`](KludgeUnknownClangTypesIntoBuiltinTypesTransformation.md)
* [`WrapNonBlittableTypesWhereNecessaryTransformation`](WrapNonBlittableTypesWhereNecessaryTransformation.md)
* [`MoveLooseDeclarationsIntoTypesTransformation`](MoveLooseDeclarationsIntoTypesTransformation.md)
* [`DeduplicateNamesTransformation`](DeduplicateNamesTransformation.md)
* [`CSharpTranslationVerifier`](CSharpTranslationVerifier.md)

Note that this order is not necessarilly especially important. Many of these transformations could easily be in a different order, although for some it's important they are where they are. This recommended order is likely to change when [#102](https://github.com/InfectedLibraries/Biohazrd/issues/102) is completed.

Some more complex transformations are actually implemented as multiple transformations internally. (For instance, [`CSharpTranslationVerifier`](CSharpTranslationVerifier.md) has a second internal stage named `CSharpTranslationVerifierPass2`.) These are considered implementation details and are generally not documented here.
