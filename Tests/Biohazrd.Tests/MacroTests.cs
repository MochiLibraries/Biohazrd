using Biohazrd.Tests.Common;
using System.IO;
using System.Linq;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class MacroTests : BiohazrdTestBase
    {
        [Fact]
        public void Basic()
        {
            TranslatedLibrary library = CreateLibrary("#define TEST 3226");
            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("A.h", Path.GetFileName(macro.File.FilePath));
            Assert.Equal("TEST", macro.Name);
            Assert.False(macro.WasUndefined);
            Assert.False(macro.IsFunctionLike);
            Assert.Empty(macro.ParameterNames);
            Assert.False(macro.LastParameterIsVardic);
            Assert.True(macro.HasValue);
            Assert.False(macro.IsUsedForHeaderGuard);
        }

        [Fact]
        public void Basic_NoValue()
        {
            TranslatedLibrary library = CreateLibrary("#define TEST");
            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("TEST", macro.Name);
            Assert.False(macro.IsFunctionLike);
            Assert.False(macro.HasValue);
        }

        [Fact]
        public void FunctionLike_NoParameters()
        {
            TranslatedLibrary library = CreateLibrary("#define TEST() 3226");
            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("TEST", macro.Name);
            Assert.True(macro.IsFunctionLike);
            Assert.Empty(macro.ParameterNames);
            Assert.False(macro.LastParameterIsVardic);
            Assert.True(macro.HasValue);
        }

        [Fact]
        public void FunctionLike_OneParameter()
        {
            TranslatedLibrary library = CreateLibrary("#define TEST(a) ((a) * 3226)");
            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("TEST", macro.Name);
            Assert.True(macro.IsFunctionLike);
            Assert.Single(macro.ParameterNames);
            Assert.Equal("a", macro.ParameterNames[0]);
            Assert.False(macro.LastParameterIsVardic);
            Assert.True(macro.HasValue);
        }

        [Fact]
        public void FunctionLike_TwoParameters()
        {
            TranslatedLibrary library = CreateLibrary("#define TEST(a, b) (((a) + (b)) * 3226)");
            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("TEST", macro.Name);
            Assert.True(macro.IsFunctionLike);
            Assert.Equal(2, macro.ParameterNames.Length);
            Assert.Equal("a", macro.ParameterNames[0]);
            Assert.Equal("b", macro.ParameterNames[1]);
            Assert.False(macro.LastParameterIsVardic);
        }

        [Fact]
        public void FunctionLike_VardicC99()
        {
            TranslatedLibrary library = CreateLibrary("#define PRINTF_WRAPPER(message, ...) printf((message), __VA_ARGS__)");
            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("PRINTF_WRAPPER", macro.Name);
            Assert.True(macro.IsFunctionLike);
            Assert.Equal(2, macro.ParameterNames.Length);
            Assert.Equal("message", macro.ParameterNames[0]);
            Assert.Equal("...", macro.ParameterNames[1]);
            Assert.True(macro.LastParameterIsVardic);
        }

        [Fact]
        public void FunctionLike_VardicC99_Single()
        {
            TranslatedLibrary library = CreateLibrary("#define PRINTF_WRAPPER(...) printf(__VA_ARGS__)");
            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("PRINTF_WRAPPER", macro.Name);
            Assert.True(macro.IsFunctionLike);
            Assert.Single(macro.ParameterNames);
            Assert.Equal("...", macro.ParameterNames[0]);
            Assert.True(macro.LastParameterIsVardic);
        }

        [Fact]
        public void FunctionLike_VardicGNU()
        {
            // This tests the GNU C extension-style vardic parameter: https://gcc.gnu.org/onlinedocs/cpp/Variadic-Macros.html
            TranslatedLibrary library = CreateLibrary("#define PRINTF_WRAPPER(message, args...) printf((message), args)");
            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("PRINTF_WRAPPER", macro.Name);
            Assert.True(macro.IsFunctionLike);
            Assert.Equal(2, macro.ParameterNames.Length);
            Assert.Equal("message", macro.ParameterNames[0]);
            Assert.Equal("args...", macro.ParameterNames[1]);
            Assert.True(macro.LastParameterIsVardic);
        }

        [Fact]
        public void FunctionLike_VardicGNU_Single()
        {
            // This tests the GNU C extension-style vardic parameter: https://gcc.gnu.org/onlinedocs/cpp/Variadic-Macros.html
            TranslatedLibrary library = CreateLibrary("#define PRINTF_WRAPPER(args...) printf(args)");
            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("PRINTF_WRAPPER", macro.Name);
            Assert.True(macro.IsFunctionLike);
            Assert.Single(macro.ParameterNames);
            Assert.Equal("args...", macro.ParameterNames[0]);
            Assert.True(macro.LastParameterIsVardic);
        }

        [Fact]
        public void OutOfScopeMacro_SkippedByDefault()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
#include ""B.h""
#define MACRO_A 100
"
            });
            builder.AddFile(new SourceFile("B.h")
            {
                Contents = @"#define MACRO_B 200",
                IndexDirectly = false,
                IsInScope = false
            });

            TranslatedLibrary library = builder.Create();
            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("MACRO_A", macro.Name);
        }

        [Fact]
        public void OutOfScopeMacro_IncludeWhenEnabled()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
#include ""B.h""
#define MACRO_A 100
"
            });
            builder.AddFile(new SourceFile("B.h")
            {
                Contents = @"#define MACRO_B 200",
                IndexDirectly = false,
                IsInScope = false
            });
            builder.Options = builder.Options with { IncludeMacrosDefinedOutOfScope = true };

            TranslatedLibrary library = builder.Create();
            Assert.Equal(2, library.Macros.Length);
            Assert.Contains(library.Macros, m => m.Name == "MACRO_A");
            Assert.Contains(library.Macros, m => m.Name == "MACRO_B");
        }

        [Fact]
        public void UndefinedMacro_SkippedByDefault()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#define TEST 3226
#undef TEST
"
            );

            Assert.Empty(library.Macros);
        }

        [Fact]
        public void UndefinedMacro_IncludedWhenEnabled()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#define TEST 3226
#undef TEST
",
                options: new TranslationOptions()
                {
                    IncludeUndefinedMacros = true
                }
            );

            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("TEST", macro.Name);
            Assert.True(macro.WasUndefined);
            Assert.True(macro.HasValue);
        }

        [Fact]
        public void UndefinedMacro_IncludedWhenEnabled_NoValue()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#define TEST
#undef TEST
",
                options: new TranslationOptions()
                {
                    IncludeUndefinedMacros = true
                }
            );

            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("TEST", macro.Name);
            Assert.True(macro.WasUndefined);
            Assert.False(macro.HasValue);
        }

        [Fact]
        public void RedefinedMacro()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#define TEST 3226
#undef TEST
#define TEST 0xC0FFEE
"
            );

            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("TEST", macro.Name);
            Assert.False(macro.WasUndefined);
        }

        [Fact]
        public void SynthesizedMacro_SkippedByDefault()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h") { Contents = "#define TEST 3226" });
            builder.AddCommandLineArgument("-DCMD_LINE");
            builder.AddCommandLineArgument("--target=x86_64-pc-win32");

            TranslatedLibrary library = builder.Create();
            TranslatedMacro macro = Assert.Single(library.Macros);
            Assert.Equal("TEST", macro.Name);
        }

        [Fact]
        public void SynthesizedMacro_IncludedWhenEnabled()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h") { Contents = "#define TEST 3226" });
            builder.AddCommandLineArgument("-DCMD_LINE");
            builder.AddCommandLineArgument("--target=x86_64-pc-win32");
            builder.Options = builder.Options with { IncludeSynthesizedMacros = true };

            TranslatedLibrary library = builder.Create();
            Assert.Contains(library.Macros, m => m.Name == "TEST");
            Assert.Contains(library.Macros, m => m.Name == "CMD_LINE"); // Macro specified on the command line
            Assert.Contains(library.Macros, m => m.Name == "_WIN32"); // Macro synthesized by Clang for Windows targets
        }

        [Fact]
        public void MacrosAreSorted()
        {
            // Macros are expected to be sorted for determinism purposes
            // Internally ClangSharp.Pathogen enumerates them from a hash map, so they're in a randomish order otherwise
            TranslatedLibrary library = CreateLibrary
            (@"
#define AAAAAAAAAAAAAAAAAB
#define AAAAAAAAAAAAAAAAAC
#define AAAAAAAAAAAAAAAAAA
",
                options: new TranslationOptions()
                {
                    IncludeSynthesizedMacros = true
                }
            );

            Assert.Equal("AAAAAAAAAAAAAAAAAA", library.Macros[0].Name);
            Assert.Equal("AAAAAAAAAAAAAAAAAB", library.Macros[1].Name);
            Assert.Equal("AAAAAAAAAAAAAAAAAC", library.Macros[2].Name);
        }

        [Fact]
        public void HeaderGuardMacro()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#ifndef __A_H__
#define __A_H__

#define OTHER_MACRO

#endif
"
            );
            TranslatedMacro headerGuard = Assert.Single(library.Macros.Where(m => m.Name == "__A_H__"));
            TranslatedMacro otherMacro = Assert.Single(library.Macros.Where(m => m.Name == "OTHER_MACRO"));
            Assert.True(headerGuard.IsUsedForHeaderGuard);
            Assert.False(otherMacro.IsUsedForHeaderGuard);
        }

        [Fact]
        public void HeaderGuardMacro_NotActuallyGuard()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#ifndef __A_H__
#define __A_H__

#define OTHER_MACRO

#endif

class Surprise { };
"
            );
            TranslatedMacro headerGuard = Assert.Single(library.Macros.Where(m => m.Name == "__A_H__"));
            TranslatedMacro otherMacro = Assert.Single(library.Macros.Where(m => m.Name == "OTHER_MACRO"));
            Assert.False(headerGuard.IsUsedForHeaderGuard);
            Assert.False(otherMacro.IsUsedForHeaderGuard);
        }

        [Fact]
        public void HeaderGuardMacro_WeirdName()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#ifndef JUSTPROVINGTHATCLANGISNOTUSINGHEURISTICS
#define JUSTPROVINGTHATCLANGISNOTUSINGHEURISTICS

#define OTHER_MACRO

#endif
"
            );
            TranslatedMacro headerGuard = Assert.Single(library.Macros.Where(m => m.Name == "JUSTPROVINGTHATCLANGISNOTUSINGHEURISTICS"));
            TranslatedMacro otherMacro = Assert.Single(library.Macros.Where(m => m.Name == "OTHER_MACRO"));
            Assert.True(headerGuard.IsUsedForHeaderGuard);
            Assert.False(otherMacro.IsUsedForHeaderGuard);
        }

        [Fact]
        public void HeaderGuardMacro_WeirdIfStyle()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#if !defined(__A_H__)
#define __A_H__

#define OTHER_MACRO

#endif
"
            );
            TranslatedMacro headerGuard = Assert.Single(library.Macros.Where(m => m.Name == "__A_H__"));
            TranslatedMacro otherMacro = Assert.Single(library.Macros.Where(m => m.Name == "OTHER_MACRO"));
            Assert.True(headerGuard.IsUsedForHeaderGuard);
            Assert.False(otherMacro.IsUsedForHeaderGuard);
        }

        [Fact]
        public void HeaderGuardMacro_WeirdWithValue()
        {
            TranslatedLibrary library = CreateLibrary
            (@"
#ifndef __A_H__
#define __A_H__ 1

#define OTHER_MACRO

#endif
"
            );
            TranslatedMacro headerGuard = Assert.Single(library.Macros.Where(m => m.Name == "__A_H__"));
            TranslatedMacro otherMacro = Assert.Single(library.Macros.Where(m => m.Name == "OTHER_MACRO"));
            Assert.True(headerGuard.IsUsedForHeaderGuard);
            Assert.False(otherMacro.IsUsedForHeaderGuard);
        }
    }
}
