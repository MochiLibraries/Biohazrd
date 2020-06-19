class VirtualClass
{
public:
    void NonVirtualMethod();
    virtual void VirtualMethod();

    virtual void OverloadedVirtualMethod();
    virtual void OverloadedVirtualMethod(int x);
};
