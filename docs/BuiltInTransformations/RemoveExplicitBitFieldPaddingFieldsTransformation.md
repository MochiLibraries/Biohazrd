`RemoveExplicitBitFieldPaddingFieldsTransformation`
===================================================================================================

<small>\[[Transformation Source](../../Biohazrd.Transformation/Common/RemoveExplicitBitFieldPaddingFieldsTransformation.cs)\]</small>

With C bit fields, you can define an unnamed fields to add explicit padding between your bit fields. You can also define a bit field with a width of 0 to enforce explicit alignment.

These fields are basically useless in the context of API consumers, so this transformation removes them.

## When this transformation is applicable

This transformation should generally always be used.

## Details

Given the following struct in C++:

```cpp
struct TestStructNoPadding
{
    int FieldA : 2;
    int FieldB : 2;
    int FieldC : 2;
};

struct TestStruct
{
    int FieldA : 2;
    int : 2; // <-- Explicit padding field
    int FieldB : 2;
    int : 0; // <-- Explicit alignment padding field
    int FieldC : 2;
};
```

Biohazrd's translation stage will output the following declaration tree:

```
TranslatedRecord TestStructNoPadding
    TranslatedBitField FieldA (Offset = 0, BitOffset = 0, BitWidth = 2)
    TranslatedBitField FieldB (Offset = 0, BitOffset = 2, BitWidth = 2)
    TranslatedBitField FieldC (Offset = 0, BitOffset = 4, BitWidth = 2)

TranslatedRecord TestStruct
    TranslatedBitField FieldA                      (Offset = 0, BitOffset = 0, BitWidth = 2)
    TranslatedBitField <>UnnamedTranslatedBitField (Offset = 0, BitOffset = 2, BitWidth = 2) ☣
    TranslatedBitField FieldB                      (Offset = 0, BitOffset = 4, BitWidth = 2)
    TranslatedBitField <>UnnamedTranslatedBitField (Offset = 4, BitOffset = 0, BitWidth = 0) ☣
    TranslatedBitField FieldC                      (Offset = 4, BitOffset = 0, BitWidth = 2)
```

This transformation removes the two unnamed bit fields (marked with ☣), resulting in the following for `TestStruct`:

```
TranslatedRecord TestStruct
    TranslatedBitField FieldA (Offset = 0, BitOffset = 0, BitWidth = 2)
    TranslatedBitField FieldB (Offset = 0, BitOffset = 4, BitWidth = 2)
    TranslatedBitField FieldC (Offset = 4, BitOffset = 0, BitWidth = 2)
```
