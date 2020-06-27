#pragma once

class MethodBeforeFirstField
{
public:
    void SomeMethod();
    int SomeField;
};

class VirtualMethodBeforeFirstField
{
public:
    virtual void SomeMethod();
    int SomeField;
};

class EmptyBase { };

class MethodBeforeFirstFieldWithEmptyBase : public EmptyBase
{
public:
    void SomeMethod();
    int SomeField;
};

class VirtualMethodBeforeFirstFieldWithEmptyBase : public EmptyBase
{
public:
    virtual void SomeMethod();
    int SomeField;
};
