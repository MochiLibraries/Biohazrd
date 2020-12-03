`PointerTypeReference`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd/#TypeReferences/PointerTypeReference.cs)\]</small>

This type represents pointers and C++ references. In the case of C++ references, `WasReference` will be true.

## Example

Given the following C++ code:

```cpp
int* GetIntPointer();
int& GetIntReference();
```

You can expect the following declaration tree after type reduction has ocurred:

```
TranslatedFunction GetIntPointer
    ReturnType: PointerTypeReference System.Int32* ☣
        Inner: CSharpBuiltinTypeReference System.Int32
TranslatedFunction GetIntReference
    ReturnType: PointerTypeReference System.Int32& (WasReference) ☣
        Inner: CSharpBuiltinTypeReference System.Int32
```

The corresponding C# variable definitions are:

```csharp
public static extern int* GetIntPointer();
public static extern int* GetIntReference();
```
