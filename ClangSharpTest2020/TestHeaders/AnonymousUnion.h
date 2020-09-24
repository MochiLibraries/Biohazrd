#pragma once

struct AnonymousUnionWithoutFieldName
{
public:
    union
    {
        int Integer;
        float Float;
    };
    int AfterUnion;
};

struct AnonymousUnionWithoutFieldName2
{
public:
    union
    {
        int Integer;
        float Float;
        union
        {
            short Short;
            char Char;
        };
    };
    int AfterUnion;
};

struct AnonymousUnionWithFieldName
{
public:
    union
    {
        int Integer;
        float Float;
    } UnionField;
    int AfterUnion;
};
