`TypeReference`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd/#TypeReferences/TypeReference.cs)\]</small>

This is the abstract base type reference type. It cannot refer to anything on its own.

This is generally the type you extend if you're implementing your own type reference type.

Unlike declarations, type references are expected to implement value equality. You generally get this for free as type references are [C#9 records](https://devblogs.microsoft.com/dotnet/c-9-0-on-the-record/#records), but if your type has inconsequential private members (such as caching for lazy evaluations) you must manually implement `Equals` and `GetHashCode` to ensure the apparent value equality works as expected.
