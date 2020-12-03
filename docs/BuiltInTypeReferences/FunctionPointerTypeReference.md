`FunctionPointerTypeReference`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd/#TypeReferences/FunctionPointerTypeReference.cs)\]</small>

This type reference represents a function pointer type. It is a composite of multiple type references, one for the return type and zero or more for the parameter types.

## Example

Given the following C code:

```c
void (*FunctionPointer)(int, int);
```

You can expect the following declaration tree after type reduction has ocurred:

```
TranslatedStaticField FunctionPointer
    Type: FunctionPointerTypeReference FuncPtr(System.Int32, System.Int32) -> void (CXCallingConv_C) ☣
        ReturnType: VoidTypeReference void
        ParameterTypes[0]: CSharpBuiltinTypeReference System.Int32
        ParameterTypes[1]: CSharpBuiltinTypeReference System.Int32
```

The corresponding C# variable definition is:

```csharp
delegate* unmanaged[Cdecl]<int, int, void>* FunctionPointer;
```
