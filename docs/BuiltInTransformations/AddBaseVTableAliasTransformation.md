`AddBaseVTableAliasTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.CSharp/#Transformations/AddBaseVTableAliasTransformation.cs)\]</small>

When a type with virtual methods inherits from another type with virtual methods, the child type has a virtual method table, but no virtual method pointer. (Because the pointer lives in the base type.)

This transformation adds virtual method pointers to these types to make it easier to access their virtual method tables when needed.

## When this transformation is applicable

This transformation should generally always be used.

The default emit strategy for virtual method trampolines assumes this transformation has been applied.

## Details

Given the following C++ code:

```cpp
class MyBaseClass
{
    virtual void VirtualMethod() = 0;
};

class MyChildClass : public MyBaseClass
{
    virtual void ChildVirtualMethod() = 0;
};
```

Biohazrd's translation stage will output the following declaration tree:

```
TranslatedRecord MyBaseClass
    TranslatedFunction    VirtualMethod
    TranslatedVTableField VirtualMethodTablePointer @ 0
    TranslatedVTable      VirtualMethodTable
        TranslatedVTableEntry __RTTI
        TranslatedVTableEntry VirtualMethod
TranslatedRecord MyChildClass
    TranslatedFunction  ChildVirtualMethod
    TranslatedBaseField Base @ 0
    TranslatedVTable    VirtualMethodTable
        TranslatedVTableEntry __RTTI
        TranslatedVTableEntry VirtualMethod
        TranslatedVTableEntry ChildVirtualMethod
```

Notice that `MyChildClass` has no `TranslatedVTableField`. This is because it gets its vtable field from the base class.

While this is more accurate to how Clang looks at things, it's not particularly intuitive to need to go through the base class to get to the vtable pointer.

After applying this transformation, the translation becomes the following:

```
TranslatedRecord MyBaseClass
    TranslatedFunction    VirtualMethod
    TranslatedVTableField VirtualMethodTablePointer @ 0
    TranslatedVTable      VirtualMethodTable
        TranslatedVTableEntry __RTTI
        TranslatedVTableEntry VirtualMethod
TranslatedRecord MyChildClass
    TranslatedFunction    ChildVirtualMethod
    TranslatedBaseField   Base @ 0
    TranslatedVTableField VirtualMethodTablePointer @ 0 ☣
    TranslatedVTable VirtualMethodTable
        TranslatedVTableEntry __RTTI
        TranslatedVTableEntry VirtualMethod
        TranslatedVTableEntry ChildVirtualMethod
```

Notice the added vtable field marked with ☣, and notice that the base field and the vtable pointer lie at the same offset. (Effectively making `MyChildClass.VirtualMethodTablePointer` an alias for `VirtualMethodTablePointer.Base.VirtualMethodTablePointer`, except typed using `MyChildClass.VirtualMethodTable` instead of `MyBaseClass.VirtualMethodTable`.)

The generated C# code will look like the following:

```csharp
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit, Size = 8)]
public unsafe partial struct MyBaseClass
{
    private unsafe void VirtualMethod()
    {
        fixed (MyBaseClass* @this = &this)
        { VirtualMethodTablePointer->VirtualMethod(@this); }
    }

    [FieldOffset(0)] internal VirtualMethodTable* VirtualMethodTablePointer;

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VirtualMethodTable
    {
        /// <summary>Virtual method pointer for `VirtualMethod`</summary>
        public delegate* unmanaged[Cdecl]<MyBaseClass*, void> VirtualMethod;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 8)]
public unsafe partial struct MyChildClass
{
    [FieldOffset(0)] internal MyBaseClass Base;

    private unsafe void ChildVirtualMethod()
    {
        fixed (MyChildClass* @this = &this)
        { VirtualMethodTablePointer->ChildVirtualMethod(@this); }
    }

    [FieldOffset(0)] internal VirtualMethodTable* VirtualMethodTablePointer; // ☣

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VirtualMethodTable
    {
        /// <summary>Virtual method pointer for `VirtualMethod`</summary>
        public delegate* unmanaged[Cdecl]<MyBaseClass*, void> VirtualMethod;
        /// <summary>Virtual method pointer for `ChildVirtualMethod`</summary>
        public delegate* unmanaged[Cdecl]<MyChildClass*, void> ChildVirtualMethod;
    }
}
```

The field marked with ☣ is the field which was added by this transformation.

Say you wanted to call `ChildVirtualMethod` directly through the vtable pointer, here's what the unsafe C# code would look like with and without this 
