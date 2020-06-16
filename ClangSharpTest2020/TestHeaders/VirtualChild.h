#include "VirtualClass.h"

class VirtualChild : public VirtualClass
{
public:
    void NonVirtualChildMethod();
    virtual void VirtualMethod();
    virtual void AddedVirtualMethod();
};
