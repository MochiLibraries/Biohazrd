`BrokenDeclarationExtractor`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.Transformation/Common/BrokenDeclarationExtractor.cs)\]</small>

This transformation looks for any declarations in the library which have associated errors and removes them.

It keeps a list of the removed declarations so you can emit the associate errors for diagnostic purposes.

## When this transformation is applicable

This transformation should generally always be used. It prevents invalid declarations from being passed to the output generation stage where undefined behavior may occur.

## Interacting with this transformation

Generally this transformation is run twice.

Once at the very beginning of the transformation pipeline before anything else: This ensures subsequent transformations do not need to worry about handling any broken declarations which failed to be translated in the first place.

The second time to run this transformation is at the very end of transformation, after the validation stage. This ensures any declarations which failed validation are not passed to the output generation stage. (Declarations with errors have undefined behavior while being emitted. Allowing them to be emitted may result in output which is not useful or fails to compile.)

You should generally inspect the `BrokenDeclarations` property of this transformation to emit diagnostics relating to the removed declarations. (IE: Using `Biohazrd.Utilities.DiagnosticWriter.AddFrom(BrokenDeclarationExtractor, string)`)
