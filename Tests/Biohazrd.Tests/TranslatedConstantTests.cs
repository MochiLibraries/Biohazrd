using Biohazrd.Expressions;
using Biohazrd.Tests.Common;
using System;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class TranslatedConstantTests : BiohazrdTestBase
    {
        [Fact]
        public void CanCreateFromConstantEvaluationResult()
        {
            TranslatedLibraryBuilder builder = new();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();
            ConstantEvaluationResult result = evaluator.Evaluate("3226");
            TranslatedConstant constant = new("Constant", result);
            Assert.Empty(constant.Diagnostics);
            IntegerConstant value = Assert.IsType<IntegerConstant>(constant.Value);
            Assert.Equal(3226UL, value.Value);
        }

        [Fact]
        public void WarningsAreObsorbedFromConstantEvaluationResult()
        {
            TranslatedLibraryBuilder builder = CreateLibraryBuilder("[[deprecated]] const int DeprecatedConstant = 3226;");
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();
            ConstantEvaluationResult result = evaluator.Evaluate("DeprecatedConstant");
            TranslatedConstant constant = new("Constant", result);
            Assert.Contains(constant.Diagnostics, d => d.Severity == Severity.Warning && d.Message.Contains("-Wdeprecated-declarations"));
            IntegerConstant value = Assert.IsType<IntegerConstant>(constant.Value);
            Assert.Equal(3226UL, value.Value);
        }

        [Fact]
        public void BrokenConstantEvaluationResultThrows()
        {
            TranslatedLibraryBuilder builder = new();
            TranslatedLibraryConstantEvaluator evaluator = builder.CreateConstantEvaluator();
            ConstantEvaluationResult result = evaluator.Evaluate("2 +");
            Assert.Throws<ArgumentException>(() => new TranslatedConstant("Constant", result));
        }
    }
}
