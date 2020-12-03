`LiftAnonymousUnionFieldsTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.Transformation/Common/LiftAnonymousUnionFieldsTransformation.cs)\]</small>

When a type contains an anonymous union in C/C++, the C/C++ compiler acts as if the fields of the union were direct members of the containing type with the same offset.

This transformation takes these anonyomous union fields lifts them into the containing type so that smilar behavior can be achieved in C#.

## When this transformation is applicable

This transformation should generally always be used unless you translate anonyomous unions some other way.

The output will still be usable without this transformation, but it will be less natural to work with it.

## Using this transformation

This transformation must occur after type reduction, otherwise it can't identify the nested union types.

## C++ background

In C/C++ if you declare an anonymous union inside a struct, it acts as if the fields of that union were fields on the struct all at the same offset.

For example:

```cpp
#include <stdio.h>
#include <stddef.h>

struct TestStruct
{
    union
    {
        int IntField;
        short ShortField;
    };
    int OtherField;
};

int main()
{
    TestStruct s;
    s.IntField = 0xC0FFEEEE;
    s.OtherField = 3226;
    printf("%ld\n", offsetof(TestStruct, IntField));   // prints 0
    printf("%ld\n", offsetof(TestStruct, ShortField)); // prints 0
    printf("%ld\n", offsetof(TestStruct, OtherField)); // prints 4

    printf("%x\n", s.IntField);   // prints 0xC0FFEEEE
    printf("%d\n", s.OtherField); // prints 3226
}
```

Notice that `IntField` appears as a field on `TestStruct` and is accessed direclty as `s.IntField` without having to go through some other field.

## Details

Given the following struct in C:

```cpp
struct TestStruct
{
    union
    {
        int IntField;
        short ShortField;
    };
    int OtherField;
};
```

Clang sees this struct as two records.

A struct record named `TestStruct` with two fields:

* `<anonymous field>` of type `<anonymous union>`
* `OtherField` of type `int`

A union record without a name (`<anonymous union>` above) with two fields:

* `IntField` of type `int`
* `ShortField` of type `short`

Naturally, Biohazrd does not attempt to obfuscate this fact from you:

```
TranslatedRecord TestStruct
    TranslatedRecord <>UnnamedTranslatedRecord
        TranslatedNormalField IntField @ 0
        TranslatedNormalField ShortField @ 0
    TranslatedNormalField <>UnnamedTranslatedNormalField @ 0
    TranslatedNormalField OtherField @ 4
```

If you attempted to generate C# for this declaration tree, you'd get something like this:

```csharp
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit, Size = 8)]
public unsafe partial struct TestStruct
{
    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public unsafe partial struct __UNICODE_003C____UNICODE_003E__UnnamedTranslatedRecord
    {
        [FieldOffset(0)] public int IntField;

        [FieldOffset(0)] public short ShortField;
    }

    [FieldOffset(0)] public __UNICODE_003C____UNICODE_003E__UnnamedTranslatedRecord __UNICODE_003C____UNICODE_003E__UnnamedTranslatedNormalField;

    [FieldOffset(4)] public int OtherField;
}
```

This is pretty ugly, but is entirely usable. Setting `ShortField` looks like this:

```csharp
TestStruct s;
s.__UNICODE_003C____UNICODE_003E__UnnamedTranslatedNormalField.ShortField = 0x3226;
```

Of course, that's pretty gnarly (and the unspeakable names certainly aren't helping.)

Once you apply this transformation, the nested union type disappears and the fields become part of the struct as you'd expect in C/C++:

```
TranslatedRecord TestStruct
    TranslatedNormalField IntField @ 0
    TranslatedNormalField ShortField @ 0
    TranslatedNormalField OtherField @ 4
```

Which results in a C# output of:

```csharp
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit, Size = 8)]
public unsafe partial struct TestStruct
{
    [FieldOffset(0)] public int IntField;

    [FieldOffset(0)] public short ShortField;

    [FieldOffset(4)] public int OtherField;
}
```

and nice, natural access:

```csharp
TestStruct s;
s.ShortField = 0x3226;
```
