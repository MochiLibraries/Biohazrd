`CSharpBuiltinTypeReference`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.CSharp/#TypeReferences/CSharpBuiltinTypeReference.cs)\]</small>

This type references refers to a built-in C# type, such as `int`, `ulong`, or `char`.

You cannot create instances of this type reference directly. Instead use the `Reference` property of one of the pre-defined `CSharpBuiltinType` types, IE: `CSharpBuiltinType.Byte.Reference`.

## Example

Given the following C code:

```c
int GetFavoriteNumber();
```

You can expect the following declaration tree after type reduction has ocurred:

```
TranslatedFunction GetFavoriteNumber
    ReturnType: CSharpBuiltinTypeReference System.Int32 ☣
```

The corresponding C# variable definition is:

```csharp
public static extern int GetFavoriteNumber();
``
