`NativeCharDeclaration`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.CSharp/#Declarations/NativeCharDeclaration.cs)\]</small>

This declaration represents the `NativeChar` type synthesized as needed by [`WrapNonBlittableTypesWhereNecessaryTransformation`](../BuiltinTransformations/WrapNonBlittableTypesWhereNecessaryTransformation.md).

## Interacting with this declarations

Your can rename this declaration if you wish to rename the generated type.

The emitted declaration is a `partial struct`, so you can add to the default implementation by either emitting or manually writing your own `partial struct` with the same name.

If you wish to modify the implementation at a fundamental level, you can replace this declaration with your own.

## Generated code

At the time of writing, this is the C# code generated for this declaration:

```csharp
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public readonly partial struct NativeChar : IComparable, IComparable<char>, IEquatable<char>, IComparable<NativeChar>, IEquatable<NativeChar>
{
    private readonly char Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private NativeChar(char value)
        => Value = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator char(NativeChar c)
        => c.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator NativeChar(char c)
        => new NativeChar(c);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(NativeChar a, NativeChar b)
        => a.Value == b.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(NativeChar a, NativeChar b)
        => a.Value != b.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(char a, NativeChar b)
        => a == b.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(char a, NativeChar b)
        => a != b.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(NativeChar a, char b)
        => a.Value == b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(NativeChar a, char b)
        => a.Value != b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
        => Value.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
        => obj switch
        {
            char character => this == character,
            NativeChar nativeChar => this == nativeChar,
            _ => false
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(char other)
        => this == other;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(NativeChar other)
        => this == other;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(object? obj)
        => Value.CompareTo(obj);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(char other)
        => Value.CompareTo(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(NativeChar value)
        => Value.CompareTo(value.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString()
        => Value.ToString();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(IFormatProvider? provider)
        => Value.ToString(provider);
}
```

This implementation is designed to incur as little performance overhead as possible. ([#110](https://github.com/InfectedLibraries/Biohazrd/issues/110) tracks investigating/reporting the .NET runtime failing to enregister this struct when it could/should.)
