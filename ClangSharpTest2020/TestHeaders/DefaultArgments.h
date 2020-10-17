enum class DefaultArgumentsTestEnum
{
    Red,
    Green,
    Blue
};

int Add(int a, int b)
{
    return a + b;
}

static const char* GlobalConstant = "What's up, world?";

void FunctionWithDefaultArguments
(
    unsigned char u8 = 0xFF,
    unsigned short u16 = 0xFFFF,
    unsigned int u32 = 0xFFFFFFFF,
    unsigned long long u64 = 0xFFFFFFFFFFFFFFFF,
    signed char s8 = 0x80,
    signed short s16 = 0x8000,
    signed int s32 = 0x80000000,
    signed long long s64 = 0x8000000000000000,
    float f32 = 3226.123456789f,
    double f64 = 3226.123456789,
    const char* str = nullptr,
    DefaultArgumentsTestEnum enumParam = DefaultArgumentsTestEnum::Blue,
    const char* str2 = "Hello, world!",
    const char* str3 = "Hello, " "world!",
    const wchar_t* utf16 = L"こんにち, world!",
    const char* str4 = GlobalConstant,
    int notDefaultableInCSharp = Add(3226, 0xC0FFEE)
);

void FunctionWithDefaultArguments2
(
    int notDefaultableInCSharp = Add(3226, 0xC0FFEE),
    unsigned char u8 = 1
);

void FunctionWithDefaultStrings
(
    const char* ascii = "Hello, world!",
    const wchar_t* wide = L"👩🏻‍💻 こんにち, world!",
    const char* utf8 = u8"👩🏻‍💻 こんにち, world!",
    const char16_t* utf16 = u"👩🏻‍💻 こんにち, world!",
    const char32_t* utf32 = U"👩🏻‍💻 こんにち, world!"
);
