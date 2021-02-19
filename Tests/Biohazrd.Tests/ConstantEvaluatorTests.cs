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
    }
}
