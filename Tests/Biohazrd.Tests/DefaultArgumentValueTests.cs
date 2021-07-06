using Biohazrd.Expressions;
using Biohazrd.Tests.Common;
using System;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class DefaultArgumentValueTests : BiohazrdTestBase
    {
        private TConstant CompileAndGetDefaultArgumentValue<TConstant>(string prefix, string parameter)
            where TConstant : ConstantValue
        {
            TranslatedLibrary library = CreateLibrary($"{prefix}\nvoid Function({parameter});");
            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("Function");
            Assert.Single(function.Parameters);
            ConstantValue? defaultValue = function.Parameters[0].DefaultValue;
            Assert.IsType<TConstant>(defaultValue);
            return (TConstant)defaultValue;
        }

        private TConstant CompileAndGetDefaultArgumentValue<TConstant>(string parameter)
            where TConstant : ConstantValue
            => CompileAndGetDefaultArgumentValue<TConstant>("", parameter);

        [Fact]
        public void BasicDefaultArgumentValues()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
enum class DefaultArgumentsTestEnum
{
    Red,
    Green,
    Blue
};

enum class DefaultArgumentsTestEnum2 : unsigned short
{
    X,
    Y,
    Z,
    W
};

int Add(int a, int b)
{
    return a + b;
}

static const char* GlobalConstant = ""What's up, world?"";

void FunctionWithDefaultArguments
(
    bool b = true,
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
    DefaultArgumentsTestEnum2 enumParam2 = DefaultArgumentsTestEnum2::W,
    const char* str2 = ""Hello, world!"",
    const char* str3 = ""Hello, "" ""world?"",
    const wchar_t* utf16 = L""こんにちは, world!"",
    const char* str4 = GlobalConstant,
    int notDefaultableInCSharp = Add(3226, 0xC0FFEE)
);"
            );

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("FunctionWithDefaultArguments");
            ConstantValue?[] expectedValues = new ConstantValue?[]
            {
                // bool b = true,
                new IntegerConstant()
                {
                    SizeBits = 1, // Clang exposes boolean constants as 1 bit integers
                    IsSigned = false,
                    Value = 1
                },
                // unsigned char u8 = 0xFF,
                new IntegerConstant()
                {
                    SizeBits = 8,
                    IsSigned = false,
                    Value = 0xFF
                },
                // unsigned short u16 = 0xFFFF,
                new IntegerConstant()
                {
                    SizeBits = 16,
                    IsSigned = false,
                    Value = 0xFFFF
                },
                // unsigned int u32 = 0xFFFFFFFF,
                new IntegerConstant()
                {
                    SizeBits = 32,
                    IsSigned = false,
                    Value = 0xFFFF_FFFF
                },
                // unsigned long long u64 = 0xFFFFFFFFFFFFFFFF,
                new IntegerConstant()
                {
                    SizeBits = 64,
                    IsSigned = false,
                    Value = 0xFFFF_FFFF_FFFF_FFFF
                },
                // signed char s8 = 0x80,
                new IntegerConstant()
                {
                    SizeBits = 8,
                    IsSigned = true,
                    // Signed types are sign-extended to long
                    Value = 0xFFFF_FFFF_FFFF_FF80
                },
                // signed short s16 = 0x8000,
                new IntegerConstant()
                {
                    SizeBits = 16,
                    IsSigned = true,
                    Value = 0xFFFF_FFFF_FFFF_8000
                },
                // signed int s32 = 0x80000000,
                new IntegerConstant()
                {
                    SizeBits = 32,
                    IsSigned = true,
                    Value = 0xFFFF_FFFF_8000_0000
                },
                // signed long long s64 = 0x8000000000000000,
                new IntegerConstant()
                {
                    SizeBits = 64,
                    IsSigned = true,
                    Value = 0x8000_0000_0000_0000
                },
                // float f32 = 3226.123456789f,
                new FloatConstant(3226.123456789f),
                // double f64 = 3226.123456789,
                new DoubleConstant(3226.123456789),
                // const char* str = nullptr,
                NullPointerConstant.Instance,
                // DefaultArgumentsTestEnum enumParam = DefaultArgumentsTestEnum::Blue,
                new IntegerConstant()
                {
                    SizeBits = 32,
                    IsSigned = true,
                    Value = 2
                },
                // DefaultArgumentsTestEnum2 enumParam2 = DefaultArgumentsTestEnum2::Z,
                new IntegerConstant()
                {
                    SizeBits = 16,
                    IsSigned = false,
                    Value = 3
                },
                // const char* str2 = ""Hello, world!"",
                new StringConstant("Hello, world!"),
                // const char* str3 = ""Hello, "" ""world?"",
                new StringConstant("Hello, world?"),
                // const wchar_t* utf16 = L""こんにちは, world!"",
                new StringConstant("こんにちは, world!"),
                // const char* str4 = GlobalConstant,
                null, // Even though this default is constant-ish, Clang does not consider this to be a constant so we don't either
                // int notDefaultableInCSharp = Add(3226, 0xC0FFEE)
                null // Non-constant default argument value
            };

            Assert.Equal(expectedValues.Length, function.Parameters.Length);

            for (int i = 0; i < expectedValues.Length; i++)
            {
                // i is included so it's easier to tell which one failed when it does.
                Assert.Equal((i, expectedValues[i]), (i, function.Parameters[i].DefaultValue));
            }
        }

        [Fact]
        public void UnsupportedConstantExpressionBecomesDiagnostic()
        {
            // _Float16 is an unusual type that Biohazrd doesn't support (yet, whenever it does this would need to change.)
            // It's only available on ARM and SPIR, hence the forced ARM target
            // https://clang.llvm.org/docs/LanguageExtensions.html#half-precision-floating-point
            TranslatedLibrary library = CreateLibrary(@"static void Function(_Float16 x = 123.f16);", targetTriple: "armv7m-pc-linux-eabi");
            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("Function");

            // Biohazrd has a UnsupportedConstantExpression internally, but it doesn't surface on the API and instead converts it to a diagnostic.
            Assert.Single(function.Parameters);
            TranslatedParameter parammeter = function.Parameters[0];
            Assert.Null(parammeter.DefaultValue);
            Assert.Single(parammeter.Diagnostics);
            Assert.Contains("Unsupported 16 bit floating point constant", parammeter.Diagnostics[0].Message);
        }

        [Fact]
        public void StringTests()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
void Function
(
    const char* ascii = ""Hello, world!"",
    const wchar_t* wide = L""👩🏻‍💻 こんにちは, world!"",
    const char* utf8 = u8""👩🏻‍💻 こんにちは, world!"",
    const char16_t* utf16 = u""👩🏻‍💻 こんにちは, world!"",
    const char32_t* utf32 = U""👩🏻‍💻 こんにちは, world!""
);"
            );

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("Function");
            (string ParameterName, string Value)[] expectedvalues = new[]
            {
                ("ascii", "Hello, world!"),
                ("wide", "👩🏻‍💻 こんにちは, world!"),
                ("utf8", "👩🏻‍💻 こんにちは, world!"),
                ("utf16", "👩🏻‍💻 こんにちは, world!"),
                ("utf32", "👩🏻‍💻 こんにちは, world!")
            };

            Assert.Equal(expectedvalues.Length, function.Parameters.Length);

            for (int i = 0; i < expectedvalues.Length; i++)
            {
                TranslatedParameter parameter = function.Parameters[i];
                Assert.IsType<StringConstant>(parameter.DefaultValue);
                string value = ((StringConstant)parameter.DefaultValue).Value;

                Assert.Equal(expectedvalues[i], (parameter.Name, value));
            }
        }

        [Theory]
        [InlineData("x86_64-pc-win32")] // wchar_t is UTF16
        [InlineData("x86_64-pc-linux")] // wchar_t is UTF32
        [InlineData("xcore")] // wchar_t is UTF8 https://github.com/InfectedLibraries/llvm-project/blob/d9c68a325b7710b93f36f02f9c58588b3bbfcd15/clang/lib/Basic/Targets/XCore.h#L37
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/203")]
        public void StringTest_WChar(string targetTriple)
        {
            TranslatedLibrary library = CreateLibrary(@"void Test(const wchar_t* x = L""こんにちは, world! 𐍊"");", targetTriple: targetTriple);
            TranslatedParameter parameter = library.FindDeclaration<TranslatedFunction>("Test").FindDeclaration<TranslatedParameter>("x");
            StringConstant stringConstant = Assert.IsAssignableFrom<StringConstant>(parameter.DefaultValue);
            Assert.Equal("こんにちは, world! 𐍊", stringConstant.Value);
        }

        [Fact]
        public void FoldedConstantTest1()
        {
            TranslatedLibrary library = CreateLibrary(@"void Function(int x = 2 + 2);");
            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("Function");
            ConstantValue? defaultValue = function.Parameters[0].DefaultValue;
            Assert.IsType<IntegerConstant>(defaultValue);
            Assert.Equal(4ul, ((IntegerConstant)defaultValue).Value);
        }

        [Fact]
        public void FoldedConstantTest2()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
bool Test();
void Function(bool x = Test() && false);"
            );

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("Function");
            ConstantValue? defaultValue = function.Parameters[0].DefaultValue;
            Assert.IsType<IntegerConstant>(defaultValue);
            Assert.Equal(0ul, ((IntegerConstant)defaultValue).Value);
        }

        [Fact]
        public void FoldedConstantTest3()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
bool Test();
void Function(bool x = Test() || true);"
            );

            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("Function");
            ConstantValue? defaultValue = function.Parameters[0].DefaultValue;
            Assert.IsType<IntegerConstant>(defaultValue);
            Assert.Equal(1ul, ((IntegerConstant)defaultValue).Value);
        }

        //===========================================================================================================================================
        // Utilities for working with quiet vs signaling NaNs
        //===========================================================================================================================================
        // The exact definitions for qNaN vs sNaN are implementation-defined. Thankfully Intel and ARM use the same definitions.
        //
        // The Intel developer's manual Volume 1 §4.8.3.4 defines the difference between quiet and signal NaNs as follows:
        // https://software.intel.com/content/www/us/en/develop/articles/intel-sdm.html#three-volume
        // "A QNaN is a NaN with the most significant fraction bit set; an SNaN is a NaN with the most significant fraction bit clear."
        //
        // ARM defines it in IEEE 754 standard implementation choices:
        // https://developer.arm.com/documentation/ddi0274/h/programmer-s-model/compliance-with-the-ieee-754-standard/ieee-754-standard-implementation-choices
        // "A most significant fraction bit of zero indicates a Signaling NaN (SNaN). A most significant fraction bit of one indicates a Quiet NaN (QNaN)."
        static bool GetQuietNanBit(uint bitPattern)
        {
            bitPattern &= 0b0___0000_0000___1000000_00000000_00000000u;
            return bitPattern != 0u;
        }

        static bool GetQuietNanBit(ulong bitPattern)
        {
            bitPattern &= 0b0___000_0000_0000___10_0000000000_0000000000_0000000000_0000000000_0000000000ul;
            return bitPattern != 0ul;
        }

        //===========================================================================================================================================
        //  ______ _             _     _______        _
        // |  ____| |           | |   |__   __|      | |
        // | |__  | | ___   __ _| |_     | | ___  ___| |_ ___
        // |  __| | |/ _ \ / _` | __|    | |/ _ \/ __| __/ __|
        // | |    | | (_) | (_| | |_     | |  __/\__ \ |_\__ \
        // |_|    |_|\___/ \__,_|\__|    |_|\___||___/\__|___/
        //===========================================================================================================================================

        [Fact]
        public void Float_Typical1()
        {
            FloatConstant constant = CompileAndGetDefaultArgumentValue<FloatConstant>("float x = 12345.f");
            Assert.Equal(12345f, constant.Value);
        }

        [Fact]
        public void Float_Typical2()
        {
            FloatConstant constant = CompileAndGetDefaultArgumentValue<FloatConstant>("float x = 123.456f");
            Assert.Equal(123.456f, constant.Value);
        }

        [Fact]
        public void Float_FromIntegerLiteral()
        {
            FloatConstant constant = CompileAndGetDefaultArgumentValue<FloatConstant>("float x = 12345");
            Assert.Equal(12345f, constant.Value);
        }

        [Fact]
        public void Float_FromDoubleLiteral1()
        {
            FloatConstant constant = CompileAndGetDefaultArgumentValue<FloatConstant>("float x = 12345.0");
            Assert.Equal(12345f, constant.Value);
        }

        [Fact]
        public void Float_FromDoubleLiteral2()
        {
            FloatConstant constant = CompileAndGetDefaultArgumentValue<FloatConstant>("float x = 123.456");
            Assert.Equal(123.456f, constant.Value);
        }

        private void AbnormalFloatTest(string prefix, string parameterValue, float expectedValue, Func<float, bool>? sanityTest, Action<uint> bitPatternTest)
        {
            FloatConstant floatConstant = CompileAndGetDefaultArgumentValue<FloatConstant>(prefix, $"float x = {parameterValue}");

            Assert.Equal(expectedValue, floatConstant.Value);

            if (sanityTest is not null)
            { Assert.True(sanityTest(floatConstant.Value)); }

            bitPatternTest((uint)BitConverter.SingleToInt32Bits(floatConstant.Value));
        }

        private void AbnormalFloatTest(string prefix, string parameterValue, float expectedValue, Func<float, bool>? sanityTest, uint bitPattern)
            => AbnormalFloatTest(prefix, parameterValue, expectedValue, sanityTest, actualBitPattern => Assert.Equal(bitPattern, actualBitPattern));

        private void AbnormalFloatTest(string parameterValue, float expectedValue, Func<float, bool>? sanityTest, uint bitPattern)
            => AbnormalFloatTest("", parameterValue, expectedValue, sanityTest, bitPattern);

        [Fact]
        public void Float_Nan1()
            // Note: The bit pattern is different from the C# and MSVC's value for NaN. They both use 0xFFC0_0000
            // From Wikipedia: "[...]a NaN may carry other information: a sign bit (which has no meaning, but may be used by some operations)[...]"
            // Assuming this is correct, IEEE-754 does not prescribe meaning to the sign bit of a NaN, so this is simply a behavior difference between Clang and MSVC/CLR.
            => AbnormalFloatTest("#include <cmath>", "NAN", Single.NaN, Single.IsNaN, 0x7FC0_0000);

        [Fact]
        public void Float_Nan2()
            => AbnormalFloatTest("0.f / 0.f", Single.NaN, Single.IsNaN, 0x7FC0_0000);

        [Fact]
        public void Float_Nan_Quiet1()
            => AbnormalFloatTest(@"__builtin_nanf(""0x12345"")", Single.NaN, Single.IsNaN, 0x7FC1_2345);

        [Fact]
        public void Float_Nan_Quiet2()
            // This is testing an abnormal use of nanf (normally you would not specify the exponennt, sign, or is_quiet bits.)
            => AbnormalFloatTest(@"__builtin_nanf(""0x7FC12345"")", Single.NaN, Single.IsNaN, 0x7FC1_2345);

        [Fact]
        public void Float_Nan_Quiet3()
            // This tests an abnormal use of nanf with the MSB of the mantissa cleared to make a signaling NaN, but nanf is only for quiet NaNs.
            // (Note that the tested bit pattern is different from what was specified because the C compiler fixes the bad bit.)
            => AbnormalFloatTest(@"__builtin_nanf(""0x7F812345"")", Single.NaN, Single.IsNaN, 0x7FC1_2345);

        [Fact]
        public void Float_Nan_Quiet4()
            => AbnormalFloatTest("#include <limits>", "std::numeric_limits<float>::quiet_NaN()", Single.NaN, Single.IsNaN, 0x7FC0_0000);

        [FutureFact] // Needs C++20
        public void Float_Nan_Quiet5()
            => AbnormalFloatTest("#include <bit>", "std::bitcast<float>(0x7FC03226)", Single.NaN, Single.IsNaN, 0x7FC0_3226);

        [Fact]
        public void Float_Nan_Signaling1()
            // nansf is not typically exposed in the C standard library, but it is there internally.
            // Note that this builtin creates quiet NaNs on MSVC due to a compiler bug:
            // https://github.com/microsoft/STL/pull/1370#discussion_r508599671
            // If this test starts failing upon upgrading Clang it's probably a MSVC compatibility thing.
            => AbnormalFloatTest(@"__builtin_nansf(""0x12345"")", Single.NaN, Single.IsNaN, 0x7F81_2345);

        [Fact]
        public void Float_Nan_Signaling2()
            // This is testing an abnormal use of nansf (normally you would not specify the exponennt, sign, or is_quiet bits.)
            => AbnormalFloatTest(@"__builtin_nansf(""0x7F812345"")", Single.NaN, Single.IsNaN, 0x7F81_2345);

        [Fact]
        public void Float_Nan_Signaling3()
            // This tests an abnormal use of nansf with the MSB of the mantissa set to make a quiet NaN, but nansf is only for signaling NaNs.
            // (Note that the tested bit pattern is different from what was specified because the C compiler fixes the bad bit.)
            => AbnormalFloatTest(@"__builtin_nansf(""0x7FC12345"")", Single.NaN, Single.IsNaN, 0x7F81_2345);

        [Fact]
        public void Float_Nan_Signaling4()
            => AbnormalFloatTest("#include <limits>", "std::numeric_limits<float>::signaling_NaN()", Single.NaN, Single.IsNaN, bits => Assert.False(GetQuietNanBit(bits)));

        [FutureFact] // Needs C++20
        public void Float_Nan_Signaling5()
            => AbnormalFloatTest("#include <bit>", "std::bitcast<float>(0x7F803226)", Single.NaN, Single.IsNaN, 0x7F80_3226);

        [Fact]
        public void Float_PositiveInfinity1()
            => AbnormalFloatTest("#include <cmath>", "INFINITY", Single.PositiveInfinity, Single.IsPositiveInfinity, 0x7F80_0000);

        [Fact]
        public void Float_PositiveInfinity2()
            => AbnormalFloatTest("1.f / 0.f", Single.PositiveInfinity, Single.IsPositiveInfinity, 0x7F80_0000);

        [Fact]
        public void Float_PositiveInfinity3()
            => AbnormalFloatTest("#include <limits>", "std::numeric_limits<float>::infinity()", Single.PositiveInfinity, Single.IsPositiveInfinity, 0x7F80_0000);

        [Fact]
        public void Float_NegativeInfinity1()
            => AbnormalFloatTest("#include <cmath>", "-INFINITY", Single.NegativeInfinity, Single.IsNegativeInfinity, 0xFF80_0000);

        [Fact]
        public void Float_NegativeInfinity2()
            => AbnormalFloatTest("-1.f / 0.f", Single.NegativeInfinity, Single.IsNegativeInfinity, 0xFF80_0000);

        [Fact]
        public void Float_NegativeInfinity3()
            => AbnormalFloatTest("#include <limits>", "-std::numeric_limits<float>::infinity()", Single.NegativeInfinity, Single.IsNegativeInfinity, 0xFF80_0000);

        [Fact]
        public void Float_NegativeZero1()
            => AbnormalFloatTest("-0.f", -0f, Single.IsNegative, 0x8000_0000);

        [Fact]
        public void Float_NegativeZero2()
            => AbnormalFloatTest("-0.0", -0f, Single.IsNegative, 0x8000_0000);

        [Fact]
        public void Float_NegativeZero3()
            // This is a sanity check that implicit cast of -0 integer constant to float does not result in negative zero
            => AbnormalFloatTest("-0", 0f, f => !Single.IsNegative(f), 0x0000_0000);

        // C/C++ define epsilon differently than the .NET BCL does:
        // https://timsong-cpp.github.io/cppwp/n4659/support.limits#numeric.limits.members-25
        // "Machine epsilon: the difference between 1 and the least value greater than 1 that is representable."
        // This value is what the MSVC standard library uses, I tried to confirm it meets this definition but it seemingly doesn't.
        // Not sure if it's a matter of modern FPUs can handle smaller numbers and this is an outdated value or what.
        private const uint CppFloatEpsilonBits = 0x3400_0000;
        private static float CppFloatEpsilon = BitConverter.Int32BitsToSingle((int)CppFloatEpsilonBits);

        [Fact]
        public void Float_Epislon1()
            => AbnormalFloatTest("#include <float.h>", "FLT_EPSILON", CppFloatEpsilon, null, CppFloatEpsilonBits);

        [Fact]
        public void Float_Epislon2()
            => AbnormalFloatTest("#include <limits>", "std::numeric_limits<float>::epsilon()", CppFloatEpsilon, null, CppFloatEpsilonBits);

        [Fact]
        public void Float_Epislon3()
            // This uses the BCL's definition of Epsilon: The smallest value greater than zero
            => AbnormalFloatTest("1.4e-45f", Single.Epsilon, null, 0x0000_0001);

        [Fact]
        public void Float_Epislon4()
            => AbnormalFloatTest("1.40129846432e-45f", Single.Epsilon, null, 0x0000_0001);

        [Fact]
        public void Float_Epislon5()
            => AbnormalFloatTest("1.40129846432481707092372958328991613128026194187651577175706828388979108268586060148663818836212158203125E-45f", Single.Epsilon, null, 0x0000_0001);

        [Fact]
        public void Float_Epislon6()
            // This uses FLT_TRUE_MIN, which matches the BCL's definition of epsilon
            => AbnormalFloatTest("#include <float.h>", "FLT_TRUE_MIN", Single.Epsilon, null, 0x0000_0001);

        [Fact]
        public void Float_Epislon7()
            // This uses numeric_limits::denorm_min, which matches the BCL's definition of epsilon
            => AbnormalFloatTest("#include <limits>", "std::numeric_limits<float>::denorm_min()", Single.Epsilon, null, 0x0000_0001);

        [Fact]
        public void Float_MinValue()
            => AbnormalFloatTest("#include <limits>", "std::numeric_limits<float>::lowest()", Single.MinValue, null, 0xFF7F_FFFF);

        [Fact]
        public void Float_MaxValue()
            => AbnormalFloatTest("#include <limits>", "std::numeric_limits<float>::max()", Single.MaxValue, null, 0x7F7F_FFFF);

        //===========================================================================================================================================
        //  _____              _     _        _______        _
        // |  __ \            | |   | |      |__   __|      | |
        // | |  | | ___  _   _| |__ | | ___     | | ___  ___| |_ ___
        // | |  | |/ _ \| | | | '_ \| |/ _ \    | |/ _ \/ __| __/ __|
        // | |__| | (_) | |_| | |_) | |  __/    | |  __/\__ \ |_\__ \
        // |_____/ \___/ \__,_|_.__/|_|\___|    |_|\___||___/\__|___/
        //===========================================================================================================================================

        [Fact]
        public void Double_Typical1()
        {
            DoubleConstant constant = CompileAndGetDefaultArgumentValue<DoubleConstant>("double x = 12345.0");
            Assert.Equal(12345.0, constant.Value);
        }

        [Fact]
        public void Double_Typical2()
        {
            DoubleConstant constant = CompileAndGetDefaultArgumentValue<DoubleConstant>("double x = 123.456");
            Assert.Equal(123.456, constant.Value);
        }

        [Fact]
        public void Double_FromIntegerLiteral()
        {
            DoubleConstant constant = CompileAndGetDefaultArgumentValue<DoubleConstant>("double x = 12345");
            Assert.Equal(12345.0, constant.Value);
        }

        [Fact]
        public void Double_FromFloatLiteral1()
        {
            DoubleConstant constant = CompileAndGetDefaultArgumentValue<DoubleConstant>("double x = 12345.f");
            Assert.Equal((double)12345f, constant.Value);
        }

        [Fact]
        public void Double_FromFloatLiteral2()
        {
            DoubleConstant constant = CompileAndGetDefaultArgumentValue<DoubleConstant>("double x = 123.456f");
            Assert.Equal((double)123.456f, constant.Value);
        }

        private void AbnormalDoubleTest(string prefix, string parameterValue, double expectedValue, Func<double, bool>? sanityTest, Action<ulong> bitPatternTest)
        {
            DoubleConstant doubleConstant = CompileAndGetDefaultArgumentValue<DoubleConstant>(prefix, $"double x = {parameterValue}");
            Assert.Equal(expectedValue, doubleConstant.Value);

            if (sanityTest is not null)
            { Assert.True(sanityTest(doubleConstant.Value)); }

            bitPatternTest((ulong)BitConverter.DoubleToInt64Bits(doubleConstant.Value));
        }

        private void AbnormalDoubleTest(string prefix, string parameterValue, double expectedValue, Func<double, bool>? sanityTest, ulong bitPattern)
            => AbnormalDoubleTest(prefix, parameterValue, expectedValue, sanityTest, actualBitPattern => Assert.Equal(bitPattern, actualBitPattern));

        private void AbnormalDoubleTest(string parameterValue, double expectedValue, Func<double, bool>? sanityTest, ulong bitPattern)
            => AbnormalDoubleTest("", parameterValue, expectedValue, sanityTest, bitPattern);

        [Fact]
        public void Double_Nan1()
            // Note: The bit pattern is different from the C# and MSVC's value for NaN. They both use 0xFFF8_0000_0000_0000
            // From Wikipedia: "[...]a NaN may carry other information: a sign bit (which has no meaning, but may be used by some operations)[...]"
            // Assuming this is correct, IEEE-754 does not prescribe meaning to the sign bit of a NaN, so this is simply a behavior difference between Clang and MSVC/CLR.
            => AbnormalDoubleTest("#include <cmath>", "NAN", Double.NaN, Double.IsNaN, 0x7FF8_0000_0000_0000);

        [Fact]
        public void Double_Nan2()
            => AbnormalDoubleTest("0.0 / 0.0", Double.NaN, Double.IsNaN, 0x7FF8_0000_0000_0000);

        [Fact]
        public void Double_Nan_Quiet1()
            => AbnormalDoubleTest(@"__builtin_nan(""0xC0FFEE012345"")", Double.NaN, Double.IsNaN, 0x7FF8_C0FF_EE01_2345);

        [Fact]
        public void Double_Nan_Quiet2()
            // This is testing an abnormal use of nanf (normally you would not specify the exponennt, sign, or is_quiet bits.)
            => AbnormalDoubleTest(@"__builtin_nan(""0x7FF8C0FFEE012345"")", Double.NaN, Double.IsNaN, 0x7FF8_C0FF_EE01_2345);

        [Fact]
        public void Double_Nan_Quiet3()
            // This tests an abnormal use of nanf with the MSB of the mantissa cleared to make a signaling NaN, but nanf is only for quiet NaNs.
            // (Note that the tested bit pattern is different from what was specified because the C compiler fixes the bad bit.)
            => AbnormalDoubleTest(@"__builtin_nan(""0x7FF0C0FFEE012345"")", Double.NaN, Double.IsNaN, 0x7FF8_C0FF_EE01_2345);

        [Fact]
        public void Double_Nan_Quiet4()
            => AbnormalDoubleTest("#include <limits>", "std::numeric_limits<double>::quiet_NaN()", Double.NaN, Double.IsNaN, 0x7FF8_0000_0000_0000);

        [FutureFact] // Needs C++20
        public void Double_Nan_Quiet5()
            => AbnormalDoubleTest("#include <bit>", "std::bitcast<double>(0x7FF8C0FFEEEE3226)", Double.NaN, Double.IsNaN, 0x7FF8_C0FF_EEEE_3226);

        [Fact]
        public void Double_Nan_Signaling1()
            // nans is not typically exposed in the C standard library, but it is there internally.
            // Note that this builtin creates quiet NaNs on MSVC due to a compiler bug:
            // https://github.com/microsoft/STL/pull/1370#discussion_r508599671
            // If this test starts failing upon upgrading Clang it's probably a MSVC compatibility thing.
            => AbnormalDoubleTest(@"__builtin_nans(""0xC0FFEE012345"")", Double.NaN, Double.IsNaN, 0x7FF0_C0FF_EE01_2345);

        [Fact]
        public void Double_Nan_Signaling2()
            // This is testing an abnormal use of nans (normally you would not specify the exponennt, sign, or is_quiet bits.)
            => AbnormalDoubleTest(@"__builtin_nans(""0x7FF0C0FFEE012345"")", Double.NaN, Double.IsNaN, 0x7FF0_C0FF_EE01_2345);

        [Fact]
        public void Double_Nan_Signaling3()
            // This tests an abnormal use of nans with the MSB of the mantissa set to make a quiet NaN, but nans is only for signaling NaNs.
            // (Note that the tested bit pattern is different from what was specified because the compiler fixes the bad bit.)
            => AbnormalDoubleTest(@"__builtin_nans(""0x7FF8C0FFEE012345"")", Double.NaN, Double.IsNaN, 0x7FF0_C0FF_EE01_2345);

        [Fact]
        public void Double_Nan_Signaling4()
            => AbnormalDoubleTest("#include <limits>", "std::numeric_limits<double>::signaling_NaN()", Double.NaN, Double.IsNaN, bits => Assert.False(GetQuietNanBit(bits)));

        [FutureFact] // Needs C++20
        public void Double_Nan_Signaling5()
            => AbnormalDoubleTest("#include <bit>", "std::bitcast<double>(0x7FF0C0FFEEEE3226)", Double.NaN, Double.IsNaN, 0x7FF0_C0FF_EEEE_3226);

        [Fact]
        public void Double_PositiveInfinity1()
            => AbnormalDoubleTest("#include <cmath>", "INFINITY", Double.PositiveInfinity, Double.IsPositiveInfinity, 0b0___111_1111_1111___00_0000000000_0000000000_0000000000_0000000000_0000000000);

        [Fact]
        public void Double_PositiveInfinity2()
            => AbnormalDoubleTest("1.f / 0.f", Double.PositiveInfinity, Double.IsPositiveInfinity, 0b0___111_1111_1111___00_0000000000_0000000000_0000000000_0000000000_0000000000);

        [Fact]
        public void Double_PositiveInfinity3()
            => AbnormalDoubleTest("#include <limits>", "std::numeric_limits<float>::infinity()", Double.PositiveInfinity, Double.IsPositiveInfinity, 0b0___111_1111_1111___00_0000000000_0000000000_0000000000_0000000000_0000000000);

        [Fact]
        public void Double_NegativeInfinity1()
            => AbnormalDoubleTest("#include <cmath>", "-INFINITY", Double.NegativeInfinity, Double.IsNegativeInfinity, 0b1___111_1111_1111___00_0000000000_0000000000_0000000000_0000000000_0000000000);

        [Fact]
        public void Double_NegativeInfinity2()
            => AbnormalDoubleTest("-1.f / 0.f", Double.NegativeInfinity, Double.IsNegativeInfinity, 0b1___111_1111_1111___00_0000000000_0000000000_0000000000_0000000000_0000000000);

        [Fact]
        public void Double_NegativeInfinity3()
            => AbnormalDoubleTest("#include <limits>", "-std::numeric_limits<float>::infinity()", Double.NegativeInfinity, Double.IsNegativeInfinity, 0b1___111_1111_1111___00_0000000000_0000000000_0000000000_0000000000_0000000000);

        [Fact]
        public void Double_NegativeZero1()
            => AbnormalDoubleTest("-0.f", -0.0, Double.IsNegative, 0x8000_0000_0000_0000);

        [Fact]
        public void Double_NegativeZero2()
            => AbnormalDoubleTest("-0.0", -0.0, Double.IsNegative, 0x8000_0000_0000_0000);

        [Fact]
        public void Double_NegativeZero3()
            // This is a sanity check that implicit cast of -0 integer constant to float does not result in negative zero
            => AbnormalDoubleTest("-0", 0.0, f => !Double.IsNegative(f), 0x0000_0000_0000_0000);

        // C/C++ define epsilon differently than the .NET BCL does:
        // https://timsong-cpp.github.io/cppwp/n4659/support.limits#numeric.limits.members-25
        // "Machine epsilon: the difference between 1 and the least value greater than 1 that is representable."
        // This value is what the MSVC standard library uses, I tried to confirm it meets this definition but it seemingly doesn't.
        // Not sure if it's a matter of modern FPUs can handle smaller numbers and this is an outdated value or what.
        private const ulong CppDoubleEpsilonBits = 0x3CB0_0000_0000_0000;
        private static double CppDoubleEpsilon = BitConverter.Int64BitsToDouble((long)CppDoubleEpsilonBits);

        [Fact]
        public void Double_Epislon1()
            => AbnormalDoubleTest("#include <float.h>", "DBL_EPSILON", CppDoubleEpsilon, null, CppDoubleEpsilonBits);

        [Fact]
        public void Double_Epislon2()
            => AbnormalDoubleTest("#include <limits>", "std::numeric_limits<double>::epsilon()", CppDoubleEpsilon, null, CppDoubleEpsilonBits);

        [Fact]
        public void Double_Epislon3()
            // This uses the BCL's definition of Epsilon: The smallest value greater than zero
            => AbnormalDoubleTest("4.9406564584124654E-324", Double.Epsilon, null, 0x0000_0000_0000_0001);

        [Fact]
        public void Double_Epislon4()
            // This uses DBL_TRUE_MIN, which matches the BCL's definition of epsilon
            => AbnormalDoubleTest("#include <float.h>", "DBL_TRUE_MIN", Double.Epsilon, null, 0x0000_0000_0000_0001);

        [Fact]
        public void Double_Epislon5()
            // This uses numeric_limits::denorm_min, which matches the BCL's definition of epsilon
            => AbnormalDoubleTest("#include <limits>", "std::numeric_limits<double>::denorm_min()", Double.Epsilon, null, 0x0000_0000_0000_0001);

        [Fact]
        public void Double_MinValue()
            => AbnormalDoubleTest("#include <limits>", "std::numeric_limits<double>::lowest()", Double.MinValue, null, 0b1___111_1111_1110___11_1111111111_1111111111_1111111111_1111111111_1111111111);

        [Fact]
        public void Double_MaxValue()
            => AbnormalDoubleTest("#include <limits>", "std::numeric_limits<double>::max()", Double.MaxValue, null, 0b0___111_1111_1110___11_1111111111_1111111111_1111111111_1111111111_1111111111);
    }
}
