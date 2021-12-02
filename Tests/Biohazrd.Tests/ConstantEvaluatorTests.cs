using Biohazrd.Expressions;
using Biohazrd.Tests.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class ConstantEvaluatorTests : BiohazrdTestBase
    {
        [Fact]
        public void BasicTest()
        {
            TranslatedLibraryBuilder builder = new();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();
            ConstantEvaluationResult result = evaluator.Evaluate("2");
            Assert.Empty(result.Diagnostics);
            IntegerConstant value = Assert.IsType<IntegerConstant>(result.Value);
            Assert.Equal(2UL, value.Value);
        }

        [Fact]
        public void SyntaxError()
        {
            TranslatedLibraryBuilder builder = new();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();
            ConstantEvaluationResult result = evaluator.Evaluate("2 +");
            Assert.Contains(result.Diagnostics, d => d.IsError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void Warning()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder("[[deprecated]] const int DeprecatedConstant = 3226;");
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();
            ConstantEvaluationResult result = evaluator.Evaluate("DeprecatedConstant");
            Assert.Contains(result.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("-Wdeprecated-declarations"));
            IntegerConstant value = Assert.IsType<IntegerConstant>(result.Value);
            Assert.Equal(3226UL, value.Value);
        }

        [Fact]
        public void Macro()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder("#define TEST 3226");
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            TranslatedMacro macro = library.Macros.First(m => m.Name == "TEST");
            ConstantEvaluationResult result = evaluator.Evaluate(macro);
            Assert.Empty(result.Diagnostics);
            IntegerConstant value = Assert.IsType<IntegerConstant>(result.Value);
            Assert.Equal(3226UL, value.Value);
        }

        [Fact]
        public void FunctionMacro_NoParameters0()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder("#define TEST() 3226");
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            TranslatedMacro macro = library.Macros.First(m => m.Name == "TEST");
            ConstantEvaluationResult result = evaluator.Evaluate(macro);
            Assert.Empty(result.Diagnostics);
            IntegerConstant value = Assert.IsType<IntegerConstant>(result.Value);
            Assert.Equal(3226UL, value.Value);
        }

        [Fact]
        public void FunctionMacro_NoParameters1()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder("#define TEST() 3226");
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            TranslatedMacro macro = library.Macros.First(m => m.Name == "TEST");
            ConstantEvaluationResult result = evaluator.Evaluate(macro, new string[0]);
            Assert.Empty(result.Diagnostics);
            IntegerConstant value = Assert.IsType<IntegerConstant>(result.Value);
            Assert.Equal(3226UL, value.Value);
        }

        [Fact]
        public void FunctionMacro_Parameters()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder("#define TEST(x, y) ((x) + (y))");
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            TranslatedMacro macro = library.Macros.First(m => m.Name == "TEST");
            ConstantEvaluationResult result = evaluator.Evaluate(macro, "2", "7");
            Assert.Empty(result.Diagnostics);
            IntegerConstant value = Assert.IsType<IntegerConstant>(result.Value);
            Assert.Equal(9UL, value.Value);
        }

        [Fact]
        public void FunctionMacro_Vardic()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder(@"#define TEST(...) #__VA_ARGS__");
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            TranslatedMacro macro = library.Macros.First(m => m.Name == "TEST");
            ConstantEvaluationResult result = evaluator.Evaluate(macro, "hello", "world");
            Assert.Empty(result.Diagnostics);
            StringConstant value = Assert.IsType<StringConstant>(result.Value);
            Assert.Equal("hello, world", value.Value);
        }

        [Fact]
        public void FunctionMacro_Vardic_NoArguments()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder(@"#define TEST(...) #__VA_ARGS__");
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            TranslatedMacro macro = library.Macros.First(m => m.Name == "TEST");
            ConstantEvaluationResult result = evaluator.Evaluate(macro);
            Assert.Empty(result.Diagnostics);
            StringConstant value = Assert.IsType<StringConstant>(result.Value);
            Assert.Equal("", value.Value);
        }

        [Fact]
        public void FunctionMacro_Vardic_GNU()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder(@"#define TEST(args...) #args");
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            TranslatedMacro macro = library.Macros.First(m => m.Name == "TEST");
            ConstantEvaluationResult result = evaluator.Evaluate(macro, "hello", "world");
            Assert.Empty(result.Diagnostics);
            StringConstant value = Assert.IsType<StringConstant>(result.Value);
            Assert.Equal("hello, world", value.Value);
        }

        [Fact]
        public void FunctionMacro_Vardic_SomeNonVardic()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder("#define TEST(a, b, ...) #a #b #__VA_ARGS__");
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            TranslatedMacro macro = library.Macros.First(m => m.Name == "TEST");
            ConstantEvaluationResult result = evaluator.Evaluate(macro, "hello", "world", "and", "biohazrd");
            Assert.Empty(result.Diagnostics);
            StringConstant value = Assert.IsType<StringConstant>(result.Value);
            Assert.Equal("helloworldand, biohazrd", value.Value);
        }

        [Fact]
        public void FunctionMacro_NotEnoughArguments()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder("#define TEST(x, y) ((x) + (y))");
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            TranslatedMacro macro = library.Macros.First(m => m.Name == "TEST");
            Assert.Throws<ArgumentException>(() => evaluator.Evaluate(macro, "42"));
        }

        [Fact]
        public void FunctionMacro_TooManyArguments()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder("#define TEST(x, y) ((x) + (y))");
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            TranslatedMacro macro = library.Macros.First(m => m.Name == "TEST");
            Assert.Throws<ArgumentException>(() => evaluator.Evaluate(macro, "42", "3226", "1337"));
        }

        [Fact]
        public void FunctionMacro_NotEnoughArguments_Vardic()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder("#define TEST(a, b, ...) #a #b #__VA_ARGS__");
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            TranslatedMacro macro = library.Macros.First(m => m.Name == "TEST");
            Assert.Throws<ArgumentException>(() => evaluator.Evaluate(macro, "42"));
        }

        [Fact]
        public void CannotEvaluateUndefinedMacros()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder
            (@"
#define TEST 3226
#undef TEST
"
            );
            builder.Options = builder.Options with { IncludeUndefinedMacros = true };
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            TranslatedMacro macro = library.Macros.First(m => m.Name == "TEST");
            Assert.Throws<ArgumentException>(() => evaluator.Evaluate(macro));
        }

        [Fact]
        public void ForcedUndefinedMacrosDontResolve()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder
            (@"
#define TEST 3226
#undef TEST
"
            );
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            ConstantEvaluationResult result = evaluator.Evaluate("TEST");
            Assert.Contains(result.Diagnostics, d => d.IsError);
            Assert.Null(result.Value);
        }

        [Fact]
        public void RedefinedMacro()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder
            (@"
#define TEST 3226
#undef TEST
#define TEST 0xC0FFEE
"
            );
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            TranslatedMacro macro = library.Macros.First(m => m.Name == "TEST");
            ConstantEvaluationResult result = evaluator.Evaluate(macro);
            Assert.Empty(result.Diagnostics);
            IntegerConstant value = Assert.IsType<IntegerConstant>(result.Value);
            Assert.Equal(0xC0FFEEUL, value.Value);
        }

        [Fact]
        public void EvaluateBatch()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder
            (@"
#define TEST_1 111
#define TEST_2 222
#define TEST_3 333
"
            );
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            List<TranslatedMacro> macros = library.Macros.Where(m => m.Name.StartsWith("TEST_")).OrderBy(m => m.Name).ToList();
            Assert.Equal(3, macros.Count);
            ImmutableArray<ConstantEvaluationResult> results = evaluator.EvaluateBatch(macros);
            Assert.Equal(3, results.Length);

            static void AssertMacro(ConstantEvaluationResult result, string macroName, ulong expectedValue)
            {
                Assert.Equal(macroName, result.Expression);
                Assert.Empty(result.Diagnostics);
                IntegerConstant value = Assert.IsType<IntegerConstant>(result.Value);
                Assert.Equal(expectedValue, value.Value);
            }

            AssertMacro(results[0], "TEST_1", 111);
            AssertMacro(results[1], "TEST_2", 222);
            AssertMacro(results[2], "TEST_3", 333);
        }

        [Fact]
        public void EvaluateBatch_ErrorsAttributed()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder
            (@"
#define TEST_1 111
#define TEST_2 222 +
#define TEST_3 333
"
            );
            TranslatedLibrary library = builder.Create();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();

            List<TranslatedMacro> macros = library.Macros.Where(m => m.Name.StartsWith("TEST_")).OrderBy(m => m.Name).ToList();
            Assert.Equal(3, macros.Count);
            ImmutableArray<ConstantEvaluationResult> results = evaluator.EvaluateBatch(macros);
            Assert.Equal(3, results.Length);

            static void AssertMacro(ConstantEvaluationResult result, string macroName, ulong expectedValue)
            {
                Assert.Equal(macroName, result.Expression);
                Assert.Empty(result.Diagnostics);
                IntegerConstant value = Assert.IsType<IntegerConstant>(result.Value);
                Assert.Equal(expectedValue, value.Value);
            }

            Assert.Equal("TEST_2", results[1].Expression);
            Assert.Contains(results[1].Diagnostics, d => d.IsError);
            Assert.Null(results[1].Value);

            AssertMacro(results[0], "TEST_1", 111);
            AssertMacro(results[2], "TEST_3", 333);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/158")]
        public void EvaluateBatch_EmptyExpressionList()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder("#define TEST 3226");
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();
            ImmutableArray<ConstantEvaluationResult> results = evaluator.EvaluateBatch(Array.Empty<string>());
            Assert.Empty(results);
        }

        [Fact]
        public void StringConstant_Ascii()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder(@"#define TEST ""Hello, world!""");
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();
            ConstantEvaluationResult result = evaluator.Evaluate("TEST");
            Assert.Empty(result.Diagnostics);
            StringConstant value = Assert.IsType<StringConstant>(result.Value);
            Assert.Equal("Hello, world!", value.Value);
        }

        [Fact]
        public void StringConstant_AsciiBad()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder(@"#define TEST ""こんにちは, world!""");
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();
            ConstantEvaluationResult result = evaluator.Evaluate("TEST");
            Assert.Empty(result.Diagnostics);
            StringConstant value = Assert.IsType<StringConstant>(result.Value);
            // The number of question marks depends on the encoding that Biohazrd uses for the virtual file.
            // As such, we don't care about the exact number of question marks, just that there's at least 5 of them for each of the kana
            Assert.Matches(@"^\?{5,}, world!$", value.Value);
        }

        private const ulong TestStringUtf8Length = (5 * 3) + 9 + (1 * 4) + 1; // 5 hiragana characters + 9 basic latin characters + 1 gothic character + 1 null terminator
        private const ulong TestStringUtf16Length = (14 + 2 + 1) * 2; // 14 characters + 1 gothic character + 1 null terminator
        private const ulong TestStringUtf32Length = 16 * 4; // 15 characters + 1 null terminator
        private void StringConstant_UnicodeTest(string prefix, ulong expectedLength, string? targetTriple = null)
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder($@"#define TEST {prefix}""こんにちは, world! 𐍊""", targetTriple: targetTriple);
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();
            ImmutableArray<ConstantEvaluationResult> results = evaluator.EvaluateBatch(new[] { "TEST", "sizeof(TEST)" });
            Assert.True(results.All(r => r.Diagnostics.IsEmpty));
            Assert.Equal(2, results.Length);
            StringConstant value = Assert.IsType<StringConstant>(results[0].Value);
            Assert.Equal("こんにちは, world! 𐍊", value.Value);
            IntegerConstant size = Assert.IsType<IntegerConstant>(results[1].Value);
            Assert.Equal(expectedLength, size.Value);
        }

        [Theory]
        [InlineData("x86_64-pc-win32", TestStringUtf16Length)]
        [InlineData("x86_64-pc-linux", TestStringUtf32Length)]
        // xcore uses UTF8 for wchar_t
        // https://github.com/InfectedLibraries/llvm-project/blob/d9c68a325b7710b93f36f02f9c58588b3bbfcd15/clang/lib/Basic/Targets/XCore.h#L37
        [InlineData("xcore", TestStringUtf8Length)]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/203")]
        public void StringConstant_WChar(string targetTriple, uint expectedLength)
            => StringConstant_UnicodeTest("L", expectedLength, targetTriple);

        [Fact]
        public void StringConstant_Utf8()
            => StringConstant_UnicodeTest("u8", TestStringUtf8Length);

        [Fact]
        public void StringConstant_Utf16()
        => StringConstant_UnicodeTest("u", TestStringUtf16Length);

        [Fact]
        public void StringConstant_Utf32()
            => StringConstant_UnicodeTest("U", TestStringUtf32Length);

        private void CharConstantTest(string constant, ulong expectedValue, int expectedSize, string? targetTriple = null, Action<TranslatedLibraryBuilder>? builderExtra = null)
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder($@"#define TEST {constant}", targetTriple: targetTriple);
            builderExtra?.Invoke(builder);
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();
            ConstantEvaluationResult result = evaluator.Evaluate("TEST");
            Assert.Empty(result.Diagnostics);
            IntegerConstant value = Assert.IsType<IntegerConstant>(result.Value);
            Assert.Equal(expectedValue, value.Value);
            Assert.Equal(expectedSize * 8, value.SizeBits);
        }

        [Fact]
        public void CharConstant_Ascii()
            => CharConstantTest("'X'", 0x58, 1);

        [Theory]
        [InlineData("x86_64-pc-win32", 2, "人", 0x4EBA)]
        [InlineData("x86_64-pc-linux", 4, "𐍊", 0x1034A)]
        // xcore uses UTF8 for wchar_t
        // https://github.com/InfectedLibraries/llvm-project/blob/d9c68a325b7710b93f36f02f9c58588b3bbfcd15/clang/lib/Basic/Targets/XCore.h#L37
        [InlineData("xcore", 1, "X", 0x58)]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/203")]
        public void CharConstant_WChar(string targetTriple, int expectedSize, string character, ulong expectedValue)
            => CharConstantTest($"L'{character}'", expectedValue, expectedSize, targetTriple);

        [Fact]
        public void CharConstant_Utf8()
            => CharConstantTest("u8'X'", 0x58, 1, builderExtra: b => b.AddCommandLineArgument("--std=c++20"));

        [Fact]
        public void CharConstant_Utf16()
            => CharConstantTest("u'人'", 0x4EBA, 2);

        [Fact]
        public void CharConstant_Utf32()
            => CharConstantTest("U'𐍊'", 0x1034A, 4);
    }
}
