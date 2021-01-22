`AddTrampolineMethodOptionsTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.CSharp/#Transformations/AddTrampolineMethodOptionsTransformation.cs)\]</small>

Some native functions require a special helper method called a trampoline to make them easier to call from C#. This transformation applies the specified [`MethodImplOptions`](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.methodimploptions) to all functions in the entire library. Typically it is used to enable aggressive inlining.

## When this transformation is applicable

This transformation is entirely optional. Marking all functions with `MethodImplOptions.AggressiveInlining` may improve performance in libraries which require frequent use of functions via trampolines. (For example, all virtual methods require trampolines to perform the virtual method dispatch.)
