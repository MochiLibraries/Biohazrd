#pragma once

class NormalBase
{
public:
    int NormalBaseField;
    void NormalMethod();
};

class VirtualChildWithNormalBase : public NormalBase
{
public:
    int VirtualChildField;
    virtual void VirtualChildVirtualMethod();
};
