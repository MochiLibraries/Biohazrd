`TranslatedTypeReference`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd/#TypeReferences/TranslatedTypeReference.cs)\]</small>

This type reference represents a reference to a translated declaration found elsewhere in the declaration tree.

This type has to be used instead of direct references to `TranslatedDeclaration` because the reference to a give declaration changes as it is transformed.

To resolve the `TranslatedDeclaration` pointed to by a `TranslatedTypeReference`, call either `TryResolve(TranslatedLibrary)` or `TryResolve(TranslatedLibrary, out VisitorContext)`. The latter provides the context of where the declaration is defined, but is most costly to resolve. (IE: If you know you don't need the context, don't ask for it.) Note that either method might return `null` if the reference fails to resolve in the given library. This can happen if the declaration this reference describes was removed by a transformation.

Despite the name, this type can be used to refer to any arbitrary `TranslatedDeclaration` across transformations regardless of whether the declaration represents a type.

How the type reference is resolved depends on the specific implementation used. You can use one of the `TranslatedTypeReference.Create` to create the appropriate style of reference.

References surviving transformations relies on transformations being done correctly. For example, if a [`TranslatedTypedef`](../BuiltinDeclarations/TranslatedTypedef.md) is removed because it is replaced with an already-existing [`TranslatedEnum`](../BuiltinDeclarations/TranslatedEnum.md) then you must ensure that you add the typedef's `Declaration` to the enum's `SecondaryDeclarations`. ([InfectedImGui's `ImGuiEnumTransformation `](https://github.com/InfectedLibraries/InfectedImGui/blob/abaa9a27c59323919e4afa9f98c837cac1f619e5/InfectedImGui.Generator/%23Transformations/ImGuiEnumTransformation.cs) is a good example of this.) See [writing transformations](../WritingTransformations.md) for more details.

## Example

Given the following C++ code:

```cpp
struct MyStruct
{
    int x;
    int y;
};

MyStruct GetMyStruct();
```

You can expect the following declaration tree after type reduction has ocurred:

```
TranslatedRecord MyStruct
    TranslatedNormalField x @ 0
        Type: CSharpBuiltinTypeReference System.Int32
    TranslatedNormalField y @ 4
        Type: CSharpBuiltinTypeReference System.Int32
TranslatedFunction GetMyStruct
    ReturnType: ClangDeclTranslatedTypeReference `Ref resolved by MyStruct` ☣
```

(Note that the type reference is specifically defined as a [`ClangDeclTranslatedTypeReference`](ClangDeclTranslatedTypeReference.md) since that is how the reference is resolved. This should be considered an implementation detail.)

The corresponding C# variable definition is:

```csharp
public unsafe partial struct MyStruct
{
    public int x;
    public int y;
}

public static extern MyStruct GetMyStruct();
```
