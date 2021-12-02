using Biohazrd.Tests.Common;
using System;
using Xunit;

namespace Biohazrd.CSharp.Tests
{
    public sealed class CSharpGenerationOptionsTests : BiohazrdTestBase
    {
        [Fact]
        public void DefaultRuntimeAndLanguageAreNotDefault()
        {
            CSharpGenerationOptions options = new();
            // These properties internally are defaulted, but they should not ever return the default values.
            // (They should return the actual defaults.)
            Assert.NotEqual(TargetRuntime.Default, options.TargetRuntime);
            Assert.NotEqual(TargetLanguageVersion.Default, options.TargetLanguageVersion);
        }

        [Fact]
        public void DefaultRuntimeIsInferredFromLanguage()
        {
            CSharpGenerationOptions cSharp9 = new()
            {
                TargetRuntime = TargetRuntime.Default,
                TargetLanguageVersion = TargetLanguageVersion.CSharp9
            };
            Assert.Equal(TargetRuntime.Net5, cSharp9.TargetRuntime);

            CSharpGenerationOptions cSharp10 = new()
            {
                TargetRuntime = TargetRuntime.Default,
                TargetLanguageVersion = TargetLanguageVersion.CSharp10
            };
            Assert.Equal(TargetRuntime.Net6, cSharp10.TargetRuntime);
        }

        [Fact]
        public void CloneDoesNotUndefaultRuntime()
        {
            // This test ensures that if the clone constructor is ever overridden it doesn't cause the internal defaulted field to become non-default
            // (IE: It doesn't cause the clone to have a TargetRuntime of Net5 since it's what the public getter returns.)
            CSharpGenerationOptions options = new()
            {
                TargetRuntime = TargetRuntime.Default,
                TargetLanguageVersion = TargetLanguageVersion.CSharp9
            };
            Assert.Equal(TargetRuntime.Net5, options.TargetRuntime);
            options = options with { TargetLanguageVersion = TargetLanguageVersion.CSharp10 };
            Assert.Equal(TargetRuntime.Net6, options.TargetRuntime);
        }

        [Fact]
        public void CannotForceInvalidRuntime()
            => Assert.ThrowsAny<Exception>(() => new CSharpGenerationOptions() { TargetRuntime = (TargetRuntime)Int32.MaxValue });

        [Fact]
        public void DefaultLanguageVersionIsInferredFromRuntime()
        {
            CSharpGenerationOptions net5 = new()
            {
                TargetRuntime = TargetRuntime.Net5,
                TargetLanguageVersion = TargetLanguageVersion.Default
            };
            Assert.Equal(TargetLanguageVersion.CSharp9, net5.TargetLanguageVersion);

            CSharpGenerationOptions net6 = new()
            {
                TargetRuntime = TargetRuntime.Net6,
                TargetLanguageVersion = TargetLanguageVersion.Default
            };
            Assert.Equal(TargetLanguageVersion.CSharp10, net6.TargetLanguageVersion);
        }

        [Fact]
        public void CloneDoesNotUndefaultLangugeVersion()
        {
            // This test ensures that if the clone constructor is ever overridden it doesn't cause the internal defaulted field to become non-default
            // (IE: It doesn't cause the clone to have a TargetRuntime of Net5 since it's what the public getter returns.)
            CSharpGenerationOptions options = new()
            {
                TargetRuntime = TargetRuntime.Net5,
                TargetLanguageVersion = TargetLanguageVersion.Default
            };
            Assert.Equal(TargetLanguageVersion.CSharp9, options.TargetLanguageVersion);
            options = options with { TargetRuntime = TargetRuntime.Net6 };
            Assert.Equal(TargetLanguageVersion.CSharp10, options.TargetLanguageVersion);
        }

        [Fact]
        public void CannotForceInvalidVersion()
            => Assert.ThrowsAny<Exception>(() => new CSharpGenerationOptions() { TargetLanguageVersion = (TargetLanguageVersion)Int32.MaxValue });
    }
}
