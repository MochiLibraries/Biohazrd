#pragma once

class NestedTypeContainer
{
public:
    class NestedType
    {
    public:
        int NestedTypeField;
    };

    enum NestedEnum
    {
        One,
        Two,
        Three
    };

    NestedType* NestedTypePointer;
};
