class OperatorOverloads
{
public:
    // https://docs.microsoft.com/en-us/cpp/cpp/operator-overloading
    //---------------------------------------------------------------------
    // Binary operators
    //---------------------------------------------------------------------
    int operator ,(int x);
    int operator !=(int x);
    int operator %(int x);
    int operator %=(int x);
    int operator &(int x);
    int operator &&(int x);
    int operator &=(int x);
    int operator *(int x);
    int operator *=(int x);
    int operator +(int x);
    int operator +=(int x);
    int operator -(int x);
    int operator -=(int x);
    int operator ->*(int x); // Pointer-to-member selection
    int operator /(int x);
    int operator /=(int x);
    int operator <(int x);
    int operator <<(int x);
    int operator <<=(int x);
    int operator <=(int x);
    int operator =(int x);
    int operator ==(int x);
    int operator >(int x);
    int operator >=(int x);
    int operator >>(int x);
    int operator >>=(int x);
    int operator ^(int x);
    int operator ^=(int x);
    int operator |(int x);
    int operator |=(int x);
    int operator ||(int x);
    //---------------------------------------------------------------------
    // Unary operators
    //---------------------------------------------------------------------
    int operator !();
    int operator &(); // Address-of
    int operator *(); // Dereference
    int operator +(); // Unary plus
    int operator ++();
    int operator -();
    int operator --();
    int operator ->(); // Member selection -- This is erroneously labeled as binary in the documentation linked above.
    int operator ~();
    // The Microsoft documentation linked above has both a cast operator and conversion operators
    // It is unclear if the cast operator is something separate or not.
    operator float(); // Conversion
    explicit operator double(); // Explicit conversion
    //---------------------------------------------------------------------
    // Others
    //---------------------------------------------------------------------
    void operator ()(); // Function call
    void operator()(int x); // Without a space -- This was to check if Clang normalizes the names of operator overloads, and it does.
    void operator [](int x);
    void operator delete(void* x);
    void* operator new(size_t x);
};

// Loose operator overload
struct StructWithLooseOperatorOverload
{
    int x;
};

inline StructWithLooseOperatorOverload operator +(StructWithLooseOperatorOverload a, StructWithLooseOperatorOverload b)
{
    return { a.x + b.x };
}
