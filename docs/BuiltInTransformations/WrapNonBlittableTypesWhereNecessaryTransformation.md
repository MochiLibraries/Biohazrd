`WrapNonBlittableTypesWhereNecessaryTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.CSharp/#Transformations/WrapNonBlittableTypesWhereNecessaryTransformation.cs)\]</small>

This transformation applies the [workaround described in #99](https://github.com/InfectedLibraries/Biohazrd/issues/99).

In short: C# does not support configuring the .NET marshaling logic used when unmanaged function pointers are called. This means types like `bool` and `char` cannot be configured to marshal as expected for C/C++. This transformation will replace them with `NativeBoolean` and `NativeChar` types which emulate a 1-byte bool and 2-byte char in the least intrusive manner possible. (Note that `bool` and `char` are only replaced in function pointers as to avoid exposing these types as much as possible.

## When this transformation is applicable

This transformation should generally always be used unless you have explicitly replaced all instances of `bool` and `char` yourself.

Ideally this transformation eventually becomes legacy since it works around limitations in C#.

## Customization

The implementation and default name of `NativeBoolean` and `NativeChar` are determined by the [`NativeBooleanDeclaration`](../BuiltInDeclarations/NativeBooleanDeclaration.md) and [`NativeCharDeclaration`](../BuiltInDeclarations/NativeCharDeclaration.md) declarations. These declarations are automatically added to the `TranslatedLibrary` by this transformation when they are needed.

If you wish to rename the generated `NativeBoolean` or `NativeChar` type, make a transformation which looks for these declarations and transforms their `Name` property.

If you wish to add to the implementation of either type, simply add to them as you would any other `partial struct`.

If you want to replace the implementation of either type, the best course of action would be to create your own custom declarations and use a transformation to replace the ones Biohazrd added. (The existing references to these types will be updated to yours.)

## Details

Having `bool` present as a parameter or return type on an unmanaged function pointer will occasionally work, but not always. When it doesn't work, the issues can be extremely confusing and hard to debug because in some situations the .NET runtime ends up reading garbage data to determine if a boolean is true, which means it almost always reads true even when it should read false.

This happens because some C++ compilers only set the lower bits of a register when returning a boolean (IE: `mov al, 0` for `return false;`) When the .NET marshaler goes to read this boolean, it assumes a 4-byte Win32 `BOOL` and reads `eax`, which has undefined data in the upper bits.

-------

Given the following C++ code:

```cpp
typedef bool (*SomeFunctionPointer)(char16_t);

SomeFunctionPointer GetFunction();
```

After removing remaining typedefs and performing type reduction, without this transformation you end up with the following C# code:

```csharp
[DllImport("Example.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetFunction@@YAP6A_N_S@ZXZ", ExactSpelling = true)]
public static extern delegate* unmanaged[Cdecl]<char, bool>* GetFunction();
```

Note that the function pointer returned uses `char` and `bool`. If you attempted to call the returned function, the .NET marshaler would mess with the `char` parameter and `bool` return value in harmful ways.

After applying this transformation, the C# output becomes the following:

```csharp
[DllImport("Example.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?GetFunction@@YAP6A_N_S@ZXZ", ExactSpelling = true)]
public static extern delegate* unmanaged[Cdecl]<NativeChar, NativeBoolean>* GetFunction();

[StructLayout(LayoutKind.Sequential)]
public readonly partial struct NativeBoolean : IComparable, IComparable<bool>, IEquatable<bool>, IComparable<NativeBoolean>, IEquatable<NativeBoolean>
{
    private readonly byte Value;
    // ...implementation omitted for brevity...
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public readonly partial struct NativeChar : IComparable, IComparable<char>, IEquatable<char>, IComparable<NativeChar>, IEquatable<NativeChar>
{
    private readonly char Value;
    // ...implementation omitted for brevity...
}
```

Because `NativeBoolean` uses `byte` instead of `bool`, it is considered blittable the marshaler will not touch it. Similarly, because `NativeChar` is marked as `CharSet.Unicode` it is considered blittable.

In release builds, the .NET JIT will *mostly* treat these types the same as `bool` and `char` thanks to inlining. (Only "mostly" because the JIT seems to not enregister these types. See [#110](https://github.com/InfectedLibraries/Biohazrd/issues/110).)
