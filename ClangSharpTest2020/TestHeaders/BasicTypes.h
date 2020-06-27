#pragma once
#include <stdint.h>

struct BasicTypes
{
    // Implied sign
    char Char;                    // CXType_Char_S or CXType_Char_U depending on -f(no-)signed-char
    short Short;                  // CXType_Short
    int Integer;                  // CXType_Int
    long Long;                    // CXType_Long
    long long LongLong;           // CXType_LongLong

    // Unsigned
    unsigned char UChar;          // CXType_UChar
    unsigned short UShort;        // CXType_UShort
    unsigned int UInteger;        // CXType_UInt
    unsigned long ULong;          // CXType_ULong
    unsigned long long ULongLong; // CXType_ULongLong

    // Signed
    signed char SChar;            // CXType_SChar
    signed short SShort;          // CXType_Short
    signed int SInteger;          // CXType_Int
    signed long SLong;            // CXType_Long
    signed long long SLongLong;   // CXType_LongLong

    // Explicit types
    int8_t Int8;                  // CXType_Typedef -> CXType_SChar
    int16_t Int16;                // CXType_Typedef -> CXType_Short
    int32_t Int32;                // CXType_Typedef -> CXType_Int
    int64_t Int64;                // CXType_Typedef -> CXType_LongLong
    uint8_t UInt8;                // CXType_Typedef -> CXType_UChar
    uint16_t UInt16;              // CXType_Typedef -> CXType_UShort
    uint32_t UInt32;              // CXType_Typedef -> CXType_UInt
    uint64_t UInt64;              // CXType_Typedef -> CXType_ULongLong

    // Floating point
    float Float;                  // CXType_Float
    double Double;                // CXType_Double

    // Other char types
    wchar_t WChar;                // CXType_WChar
    //char8_t Char8;              // CXType_Unexposed -- UTF8, C++20 only anyway
    char16_t Char16;              // CXType_Char16
    //char32_t Char32;            // CXType_Char32 -- UTF32, not translated yet.

    // Misc
    bool Boolean;                 // CXType_Bool
    void* VoidPointer;            // CXType_Pointer -> CXType_Void
};
