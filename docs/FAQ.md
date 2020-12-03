Frequently Anticipated Questions
===============================================================================

<!-- TOC -->

- [Background](#background)
    - [Why does Biohazrd exist?](#why-does-biohazrd-exist)
- [Platform Support](#platform-support)
    - [What .NET Runtimes are supported for Biohazrd-generated libraries?](#what-net-runtimes-are-supported-for-biohazrd-generated-libraries)
    - [What platforms are supported for Biohazrd-generated libraries?](#what-platforms-are-supported-for-biohazrd-generated-libraries)
    - [What is involved in adding a new platform?](#what-is-involved-in-adding-a-new-platform)
        - [Handling ABI stuff sounds scary, how do you know it's being done correctly?](#handling-abi-stuff-sounds-scary-how-do-you-know-its-being-done-correctly)
        - [What is plan B if this strategy doesn't work?](#what-is-plan-b-if-this-strategy-doesnt-work)
- [Language Support](#language-support)
    - [Can Biohazrd be used to wrap code written in languages besides C/C++?](#can-biohazrd-be-used-to-wrap-code-written-in-languages-besides-cc)
    - [Can Biohazrd generators be written in other .NET Languages? (IE: F#, VB)](#can-biohazrd-generators-be-written-in-other-net-languages-ie-f-vb)
    - [Can Biohazrd-generated libraries be used for other .NET Languages? (IE: F#, VB)](#can-biohazrd-generated-libraries-be-used-for-other-net-languages-ie-f-vb)
    - [Could Biohazrd be used to generate bindings for non-.NET Languages?](#could-biohazrd-be-used-to-generate-bindings-for-non-net-languages)

<!-- /TOC -->

## Background

### Why does Biohazrd exist?

Biohazrd (originally very creatively named [`ClangSharpTest2020`](https://github.com/InfectedLibraries/Biohazrd/tree/e45f8a62287c8d0d6ef17a578cb407aa33b27599)) was originally created by [David Maas](https://github.com/PathogenDavid) during a midlife crisis to dispell the myth that the only feasible way to interoperate with a C++ library was to go through a C library that wrapped it. (Or to prove that the extra layer of C truely was necessary.) In particular it was developed to [wrap NVIDIA's PhysX library](https://github.com/InfectedLibraries/InfectedPhysX).

Additionally, Biohazrd was created in hopes of simplifying the development of generating bindings for C/C++ libraries to avoid the duplicated effort between many .NET interop libraries.

## Platform Support

### What .NET Runtimes are supported for Biohazrd-generated libraries?

We currently target .NET 5.

CoreRT has been tested briefly and is expected to work.

Mono (including Unity and Xamarin) is untested.

Older versions of the runtime (including .NET Framework) may work, but they are untested and supporting them is not currently a high priority. For C++ libraries, Biohazrd makes heavy use of C# 9 function pointers which may work on older runtimes but are not officially supported by Microsoft prior to .NET 5. The old-style [`UnmanagedFunctionPointerAttribute `](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedfunctionpointerattribute)-based function pointers is unlikely to ever be supported out of the box.

### What platforms are supported for Biohazrd-generated libraries?

For C++, Biohazrd currently only supports Windows x64. Support for Linux x64 and ARM is planned.

For C, Biohazrd should (in theory) work on any platform that .NET supports. (However only Windows x64 has been tested.)

### What is involved in adding a new platform?

Biohazrd's implementation means that memory layout concerns will always be handled correctly. The main effort in adding support for a new platform is implementing how the ABI works for function and method calls on that platform. This involves answering questions like: How does `this` get passed? What values are passed by value and by reference? How are values returned by reference? How do virtual methods get invokved?

This is generally simpler than it may sound because these things usually don't differ drastically between platforms, but they still need to be considered. (For example: Windows x64 passes `this` before `retbuf` and Linux x64 is the opposite.)

Alternatively there is plan B... (see below.)

#### Handling ABI stuff sounds scary, how do you know it's being done correctly?

When possible we generally try to use Clang as the bastion of truth with most things Biohazrd does. However in some cases we have to handle things ourselves because the necessary API doesn't exist or we didn't find it yet because Clang is understandably fairly large and unwieldy.

For everything Biohazrd does, we attempt to implement things based on available specifications. Despite what the naysayers around the internet may say, [C++ ABIs are fairly well documented](https://itanium-cxx-abi.github.io/cxx-abi/abi.html). Where documentation is lacking (\*cough\*Microsoft x64\*cough\*) things usually aren't that surprising if you're familiar with low level programming and can be determined by looking at the Clang source code or by reverse-engineering output from MSVC.

There are [plans](https://github.com/InfectedLibraries/Biohazrd/issues/32) to automatically verify the ABI in mass through automated means, but these are not yet implemented.

#### What is plan B if this strategy doesn't work?

So far this strategy has worked well for us, but there's always a chance we'll find some edge case or platform where it falls apart. The biggest potential problem is a situation where the C++ calling convention is too drastically different from the C calling convention. Or a platform features an unusual, optional, C calling convention which isn't supported by the .NET Runtime.

In this situation, plan B is to use Biohazrd to generate a C binding layer to allow a C++ compiler to handle all the nasty calling convention details for us. This is contrary to one of Biohazrd's primary goals, so it's plan B for a reason.

## Language Support

### Can Biohazrd be used to wrap code written in languages besides C/C++?

Short answer: No.

Long answer: Biohazrd is built on Clang, which primarily supports C, C++, Objective C, and Objective C++. While in theory it could be modified to use something other than Clang, the [object model](BuiltInDeclarations/) is definitely designed around representing C/C++ concepts. Other languages would likely be better served by an object model designed with them in mind.

That being said, if a language is similar enough to C++ (or a subset of it) it may be feasible to support it in Biohazrd. At the very least Biohazrd could serve as a guide design to immitate for a similar framework targeting that language. ([Wouldn't hurt to ask](https://github.com/InfectedLibraries/Biohazrd/issues/new). At the very least it'd be interesting to hear about what you're trying to do.)

### Can Biohazrd generators be written in other .NET Languages? (IE: F#, VB)

Maybe! Definitely not recommended though. Biohazrd was not designed to be used from these languages, but basic usage of the framework from these libraries should be fine.

Biohazrd does make heavy use C# 9 Records to implement its immutable data model. This may affect the ability for non-C# languages to author transformations or to extend Biohazrd. (Visual Basic in particular is known to have issues with init-only properties.)

It'd definitely be interesting to experiment with writing transformations in F#. If anyone attempts this be sure to [open an issue](https://github.com/InfectedLibraries/Biohazrd/issues/new) highlighting your experience.

### Can Biohazrd-generated libraries be used for other .NET Languages? (IE: F#, VB)

Maybe! Other .NET languages typically have weaker support for unsafe code, so they probably can't work with the unsafe interop layers.

For generators authored to generate safe interop layers, it could be feasible to consume them from other languages assuming the safe interop layer is "safe enough".

### Could Biohazrd be used to generate bindings for non-.NET Languages?

Currently we only support generating C#, but Biohazrd is deliberately designed to allow extending it to generate bindings for other languages too.

For instance, you could (in theory) use Biohazrd to generate a C wrapper for a C++ library, Rust FFI, a JNI library for Java, Lua `CFunction`s, etc.

To get a rough idea of the amount of effort involved in adding a new output language, see `Biohazrd.CSharp`. You should most likely be intimately familiar with manually writing binding code for C libraries in your selected language before beginning such an endeavour.
