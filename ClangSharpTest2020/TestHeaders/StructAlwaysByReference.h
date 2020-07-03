// This file tests user-defined types which will always be passed and returned by reference

// This struct has to be passed by reference because it's too large to fit into a register
struct TooBig
{
    int x;
    int y;
    int z;

    // void Test(TooBig* this, TooBig* retbuf, TooBig* other);
    TooBig Test(TooBig other);
    virtual TooBig VirtualTest(TooBig other);
};

// void LooseTooBig(TooBig* retbuf);
TooBig LooseTooBig();

// This struct has to be passed by reference because it isn't a power of two in size
struct Npot
{
    unsigned char x;
    unsigned char y;
    unsigned char z;

    // void Test(Npot* this, Npot* retbuf, Npot* other);
    Npot Test(Npot other);
};

// void LooseNpot(Npot* retbuf);
Npot LooseNpot();

// This struct has to be passed by reference because it has a copy constructor
// Note that in this case, C# has no way of knowing that this struct has to be passed by reference.
struct CopyConstructor
{
    int x;

    CopyConstructor(CopyConstructor& other) { x = other.x; }

    // void Test(CopyConstructor* this, CopyConstructor* retbuf, CopyConstructor* other);
    CopyConstructor Test(CopyConstructor other);
};

// void LooseCopyConstructor(CopyConstructor* retbuf);
CopyConstructor LooseCopyConstructor();
