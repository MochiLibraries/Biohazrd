struct BitField0
{
    unsigned int X : 4;
    unsigned int Y : 4;
    unsigned int Z : 4;
    unsigned int W : 4;
};

struct BitField1
{
    unsigned short X : 4;
    unsigned short Y : 4;
    unsigned short Z : 4;
    unsigned short W : 4;
};

struct BitField2
{
    unsigned int X : 9;
    unsigned int Y : 16;
    unsigned char After;
};

struct BitField3
{
    unsigned int X : 9;
    unsigned int Y : 23;
    unsigned char After;
};

struct BitField4
{
    unsigned char X;
    unsigned short Y : 9;
    unsigned short Z : 16;
};

struct BitField5
{
    unsigned int X : 4;
    unsigned int : 4;
    unsigned int Y : 4;
    unsigned int : 4;
    unsigned int Z : 4;
    unsigned int : 4;
    unsigned int W : 4;
};

struct BitField6
{
    unsigned int X : 4;
    unsigned int : 4;
    unsigned int Y : 4;
    unsigned int : 4;
    unsigned int Z : 4;
    unsigned int : 0;
    unsigned int W : 4;
};

struct BitField7
{
    unsigned char uc : 1;
    unsigned char uc2 : 7;
    unsigned short us : 1;
    unsigned short us2 : 15;
    unsigned int ui : 1;
    unsigned int ui2 : 31;
    unsigned long long ul : 1;
    unsigned long long ul2 : 63;
    signed char sc : 1;
    signed char sc2 : 7;
    signed short ss : 1;
    signed short ss2 : 15;
    signed int si : 1;
    signed int si2 : 31;
    signed long long sl : 1;
    signed long long sl2 : 63;
};

enum class SomeEnum : int
{
    A,
    B,
    C
};

struct BitField8
{
    int intField : 8;
    SomeEnum enumField : 8;
};

enum SignedEnum
{
    A = -1,
    B = 0,
    C = 1,
};

struct BitField9
{
    int intField : 8;
    SignedEnum enumField : 8;
};
