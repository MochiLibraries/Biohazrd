#pragma once

class FieldWithSameNameAsType
{
public:
    // Fields in C++ can have the same name as their enclosing type, but this isn't allowed in C#
    // This checks if the translator properly avoids CS0542 (member names cannot be the same as their enclosing type)
    int FieldWithSameNameAsType;
};
