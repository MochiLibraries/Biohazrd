using Biohazrd.Tests.Common;
using ClangSharp;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class TranslatedFunctionTests : BiohazrdTestBase
    {
        [Fact]
        public void IsInline_NonInline()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#include <stddef.h> // Required for size_t on non-Windows platforms

void LooseFunction();
void* operator new(size_t, void*);

class MyClass
{
public:
    MyClass();
    void MyMethod();
    ~MyClass();
    bool operator ==(MyClass&);
    operator bool();
    static void MyStaticMethod();
};
"
            );

            int functionCount = 0;
            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (declaration is not TranslatedFunction function)
                { continue; }

                functionCount++;
                Assert.False(function.IsInline);
            }

            Assert.Equal(8, functionCount);
        }

        [Fact]
        public void IsInline_ExplicitInline()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#include <stddef.h> // Required for size_t on non-Windows platforms

inline void LooseFunction() { }
inline void* operator new(size_t, void*) { return nullptr; }

class MyClass
{
public:
    inline MyClass() { }
    inline void MyMethod() { }
    inline ~MyClass() { }
    inline bool operator ==(MyClass&) { return true; }
    inline operator bool() { return false; }
    inline static void MyStaticMethod() { }
};
"
            );

            int functionCount = 0;
            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (declaration is not TranslatedFunction function)
                { continue; }

                functionCount++;
                Assert.True(function.IsInline);
            }

            Assert.Equal(8, functionCount);
        }

        [Fact]
        public void IsInline_MsvcForceInline()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
__forceinline void LooseFunction() { }
__forceinline void* operator new(size_t, void*) { return nullptr; }

class MyClass
{
public:
    __forceinline MyClass() { }
    __forceinline void MyMethod() { }
    __forceinline ~MyClass() { }
    __forceinline bool operator ==(MyClass&) { return true; }
    __forceinline operator bool() { return false; }
    __forceinline static void MyStaticMethod() { }
};
",
                targetTriple: "x86_64-pc-win32"
            );

            int functionCount = 0;
            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (declaration is not TranslatedFunction function)
                { continue; }

                functionCount++;
                Assert.True(function.IsInline);
            }

            Assert.Equal(8, functionCount);
        }

        [LinuxFact] // The stddef.h will fail on Windows.
        public void IsInline_GccAlwaysInline()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#include <stddef.h> // Required for size_t on non-Windows platforms

__attribute__((always_inline)) inline void LooseFunction() { }
__attribute__((always_inline)) inline void* operator new(size_t, void*) { return nullptr; }

class MyClass
{
public:
    __attribute__((always_inline)) inline MyClass() { }
    __attribute__((always_inline)) inline void MyMethod() { }
    __attribute__((always_inline)) inline ~MyClass() { }
    __attribute__((always_inline)) inline bool operator ==(MyClass&) { return true; }
    __attribute__((always_inline)) inline operator bool() { return false; }
    __attribute__((always_inline)) inline static void MyStaticMethod() { }
};
",
                targetTriple: "x86_64-pc-linux"
            );

            int functionCount = 0;
            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (declaration is not TranslatedFunction function)
                { continue; }

                functionCount++;
                Assert.True(function.IsInline);
            }

            Assert.Equal(8, functionCount);
        }

        [Fact]
        public void IsInline_ImplicitInline()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#include <stddef.h> // Required for size_t on non-Windows platforms

void LooseFunction() { }
void* operator new(size_t, void*) { return nullptr; }

class MyClass
{
public:
    MyClass() { }
    void MyMethod() { }
    ~MyClass() { }
    bool operator ==(MyClass&) { return true; }
    operator bool() { return false; }
    static void MyStaticMethod() { }
};
"
            );

            int functionCount = 0;
            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (declaration is not TranslatedFunction function)
                { continue; }

                // Class methods with bodies are implicitly inline (n4659§12.2.1 Member Functions)
                // https://timsong-cpp.github.io/cppwp/n4659/class.mfct#1
                if (function.Declaration is CXXMethodDecl)
                { Assert.True(function.IsInline); }
                else
                { Assert.False(function.IsInline); }
                functionCount++;
            }

            Assert.Equal(8, functionCount);
        }

        [Fact]
        public void SpecialFunctionKind_NormalFunction()
        {
            TranslatedLibrary library = CreateLibrary("void Function();");
            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.None, function.SpecialFunctionKind);
        }

        [Fact]
        public void SpecialFunctionKind_NormalMethod()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class MyClass
{
public:
    void Method();
};
"
            );
            TranslatedFunction method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.None, method.SpecialFunctionKind);
        }

        [Fact]
        public void SpecialFunctionKind_Constructor()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class MyClass
{
public:
    MyClass();
};
"
            );
            TranslatedFunction method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.Constructor, method.SpecialFunctionKind);
        }

        [Fact]
        public void SpecialFunctionKind_Destructor()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class MyClass
{
public:
    ~MyClass();
};
"
            );
            TranslatedFunction method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.Destructor, method.SpecialFunctionKind);
        }

        [Fact]
        public void SpecialFunctionKind_OperatorOverloadFunction()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
struct MyStruct { };
bool operator==(MyStruct, MyStruct);
"
            );
            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.OperatorOverload, function.SpecialFunctionKind);
        }

        [Fact]
        public void SpecialFunctionKind_OperatorOverloadMethod()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class MyClass
{
public:
    int operator[](int i);
};
"
            );
            TranslatedFunction method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.OperatorOverload, method.SpecialFunctionKind);
        }

        [Fact]
        public void SpecialFunctionKind_ConversionOverload()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
class MyClass
{
public:
    operator int();
};
"
            );
            TranslatedFunction method = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>();
            Assert.Equal(SpecialFunctionKind.ConversionOverload, method.SpecialFunctionKind);
        }
    }
}
