namespace Biohazrd.CSharp;

public enum ReferenceTypeOutputBehavior
{
    /// <summary>C++ references will be emitted as pointers -- <c>ImVec2&</c> becomes <c>ImVec2*</c></summary>
    AsPointer,
    /// <summary>Const C++ references will be emitted as <c>ref</c> and non-const references will be passed by value</summary>
    /// <remarks>
    /// <c>ImVec&</c> will be passed by reference, IE: <c>ref ImVec</c>
    ///
    /// <c>const ImVec&</c> will be passed by value, IE: <c>ImVec</c>
    /// </remarks>
    AsRefOrByValue,
    /// <summary>All C++ references will be emitted as C# by-reference parameters</summary>
    /// <remarks>
    /// <c>ImVec&</c> will be passed by reference, IE: <c>ref ImVec</c>
    ///
    /// <c>const ImVec&</c> will be passed by read-only reference, IE: <c>in ImVec</c>
    /// </remarks>
    AlwaysByRef
}
