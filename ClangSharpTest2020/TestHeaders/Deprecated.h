#define DEPRECATED __declspec(deprecated)
#define DEPRECATED_MESSAGE(REASON) __declspec(deprecated(REASON))

class DEPRECATED Deprecated
{
public:
    DEPRECATED void Method();
    DEPRECATED int field;
};

class DEPRECATED_MESSAGE("This class is deprecated") DeprecatedWithReason
{
public:
    DEPRECATED_MESSAGE("This method is deprecated") void Method();
    DEPRECATED_MESSAGE("This field is deprecated") int field;
};

class [[deprecated]] DeprecatedCpp14
{
public:
    [[deprecated]] void Method();
    [[deprecated]] int field;
};
