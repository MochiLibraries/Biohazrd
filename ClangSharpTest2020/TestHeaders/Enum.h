#pragma once

enum MyEnum
{
    Apple,
    Orange,
    Banana
};

enum class MyEnumClass
{
    Red,
    Green = 1, // <-- Explicit value that is the same as the automatic one.
    Blue,
    Yellow = 99,
};

enum class MyEnumClassShort : short
{
    Hello = 99,
    World = 0x3226,
    Signed = -1,
    SignedHex = (short)0xFF00, // <-- This tests that the translator adds the unchecked cast
    TooBig = (short)0xF0001, // <-- Clang will implicitly truncat this value for us
};

enum class MyEnumClassUnsignedShort : unsigned short
{
    Hello = 99,
    World = 0x3226,
    MaxValue = 65535,
    Signed = (unsigned short)-1,
    SignedHex = 0xFF00,
};

enum EmptyEnum { };

// By default, libclang will sign-extend all enum constant values (regardless of whether the enum type is signed) to 64 bits.
// These enums test our translation in those situations.
// (In our case, we just ended up adding a function to libclang that gets the value without the sign extension.)
enum WillCauseSignExtensionEnum
{
    WillCauseSignExtension = 0xFF000000
};

enum WillCauseSignExtensionEnum2 : unsigned int
{
    WillCauseSignExtension2 = 0xFF000000
};

enum class WillCauseSignExtensionEnumLongLong : long long
{
    WillCauseSignExtension = 0xFF000000
};
