using Biohazrd.Tests.Common;
using System.IO;
using Xunit;

namespace Biohazrd.OutputGeneration.Tests
{
    public sealed class CppCodeWriterTests : BiohazrdCodeWriterTestBase<CppCodeWriter>
    {
        [Fact]
        public void HeaderComment()
        {
            CodeWriterTest
            (
@"// GENERATED
// FILE
#include ""Test.h""

#include <SystemTest.h>

",
                session => session.GeneratedFileHeader = "GENERATED\nFILE",
                writer =>
                {
                    writer.Include("Test.h");
                    writer.Include("SystemTest.h", systemInclude: true);
                }
            );
        }

        [Fact]
        public void Include()
        {
            CodeWriterTest
            (
@"#include ""Test.h""

// SomeCode
",
                writer =>
                {
                    writer.WriteLine("// SomeCode");
                    writer.Include("Test.h"); // Intentionally include after writing code to show include goes to top of file
                }
            );
        }

        [Fact]
        public void Include_System()
        {
            CodeWriterTest
            (
@"#include <Test.h>

// SomeCode
",
                writer =>
                {
                    writer.WriteLine("// SomeCode");
                    writer.Include("Test.h", systemInclude: true); // Intentionally include after writing code to show include goes to top of file
                }
            );
        }

        [Fact]
        public void Include_NormalAndSystem()
        {
            CodeWriterTest
            (
@"#include ""Test.h""

#include <SystemTest.h>

// SomeCode
",
                writer =>
                {
                    writer.WriteLine("// SomeCode");
                    writer.Include("SystemTest.h", systemInclude: true); // Intentionally include after writing code to show include goes to top of file
                    writer.Include("Test.h"); // Intentionally include after system include to show normal includes go before system includes
                }
            );
        }

        [Fact]
        public void IncludesAreSorted()
        {
            CodeWriterTest
            (
@"#include ""A.h""
#include ""B.h""
#include ""C.h""

#include <SystemA.h>
#include <SystemB.h>
#include <SystemC.h>

// SomeCode
",
                writer =>
                {
                    writer.WriteLine("// SomeCode");
                    writer.Include("C.h");
                    writer.Include("A.h");
                    writer.Include("B.h");

                    writer.Include("SystemC.h", systemInclude: true);
                    writer.Include("SystemA.h", systemInclude: true);
                    writer.Include("SystemB.h", systemInclude: true);
                }
            );
        }

        [Fact]
        public void IncludesAreMadeRelative()
        {
            CodeWriterTest
            (
// https://github.com/InfectedLibraries/Biohazrd/issues/165 will change the directory separator to / here
$@"#include ""..{Path.DirectorySeparatorChar}Test.h""

#include <..{Path.DirectorySeparatorChar}SystemTest.h>

// SomeCode
",
                (writer, path) =>
                {
                    string upOneDirectoryPath = Path.GetFullPath(Path.GetDirectoryName(Path.GetDirectoryName(path)!)!);
                    Assert.True(Path.IsPathRooted(upOneDirectoryPath)); // Sanity

                    writer.WriteLine("// SomeCode");
                    writer.Include(Path.Combine(upOneDirectoryPath, "SystemTest.h"), systemInclude: true);
                    writer.Include(Path.Combine(upOneDirectoryPath, "Test.h"));
                }
            );
        }

        [Fact]
        public void IncludesAreMadeRelative_FileInSubdirectory()
        {
            // This tests that the includes are relative to the actual file rather than the output session base directory
            OutputSession outputSession = null!;
            string code = FillCodeWriterAndGetCode
            (
                fileName: "subdir/output.cpp",
                session => outputSession = session,
                (writer, path) =>
                {
                    string directoryPath = Path.GetFullPath(Path.Combine(outputSession.BaseOutputDirectory, ".."));
                    Assert.True(Path.IsPathRooted(directoryPath)); // Sanity

                    writer.WriteLine("// SomeCode");
                    writer.Include(Path.Combine(directoryPath, "SystemTest.h"), systemInclude: true);
                    writer.Include(Path.Combine(directoryPath, "Test.h"));
                }
            );

            Assert.Equal
            (
// https://github.com/InfectedLibraries/Biohazrd/issues/165 will change the directory separator to / here
$@"#include ""..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}Test.h""

#include <..{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}SystemTest.h>

// SomeCode
",
                code,
                ignoreLineEndingDifferences: true
            );
        }

        [FutureFact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/165")]
        public void IncludePathsAreNormalizedToForwardSlash1()
        {
            CodeWriterTest
            (
@"#include ""Hello/World.h""

#include <Hello/System.h>

",
                writer =>
                {
                    writer.Include(@"Hello\World.h");
                    writer.Include(@"Hello\System.h", systemInclude: true);
                }
            );
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/165")]
        public void IncludePathsAreNormalizedToForwardSlash2()
        {
            CodeWriterTest
            (
@"#include ""Hello/World.h""

#include <Hello/System.h>

",
                writer =>
                {
                    writer.Include(@"Hello/World.h");
                    writer.Include(@"Hello/System.h", systemInclude: true);
                }
            );
        }

        [Fact]
        public void IncludesDeduplicated()
        {
            CodeWriterTest
            (
@"#include ""Test1.h""
#include ""Test2.h""

#include <SystemTest1.h>
#include <SystemTest2.h>

",
                writer =>
                {
                    writer.Include("Test1.h");
                    writer.Include("Test2.h");
                    writer.Include("SystemTest1.h", systemInclude: true);
                    writer.Include("SystemTest2.h", systemInclude: true);

                    writer.Include("Test1.h");
                    writer.Include("Test2.h");
                    writer.Include("SystemTest1.h", systemInclude: true);
                    writer.Include("SystemTest2.h", systemInclude: true);
                }
            );
        }

        [Fact]
        public void IncludesNotDeduplicatedAgainstSystemIncludes()
        {
            // Since the compiler behavior between lookup of system and non-system includes is different, we assume it's intentional if the "same" file is
            // included twice as system and non-system.
            CodeWriterTest
            (
@"#include ""Test.h""

#include <Test.h>

",
                writer =>
                {
                    writer.Include("Test.h");
                    writer.Include("Test.h", systemInclude: true);
                }
            );
        }

        [Fact]
        public void IncludesDeduplicationIsCaseSensitive()
        {
            CodeWriterTest
            (
@"#include ""Test1.h""
#include ""TEST1.h""

#include <SystemTest1.h>
#include <SYSTEMTEST1.h>

",
                writer =>
                {
                    writer.Include("Test1.h");
                    writer.Include("TEST1.h");
                    writer.Include("SystemTest1.h", systemInclude: true);
                    writer.Include("SYSTEMTEST1.h", systemInclude: true);
                }
            );
        }

        [Fact]
        public void DisableScope()
        {
            CodeWriterTest
            (
// The extra newline after LINE1 may seem odd, but the DisabledScope ensures separation since it's intended to be used to disable a declaration
@"LINE1

#if 0
DISABLED1
DISABLED2
#endif
LINE2
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.DisableScope())
                    {
                        writer.EnsureSeparation(); // This is expected to do nothing since we're "part" of the disabled block
                        writer.WriteLine("DISABLED1");
                        writer.WriteLine("DISABLED2");
                    }
                    writer.WriteLine("LINE2");
                }
            );
        }

        [Fact]
        public void DisableScope_NoSeparation()
        {
            CodeWriterTest
            (
@"LINE1
#if 0
DISABLED1
DISABLED2
#endif
LINE2
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    writer.NoSeparationNeededBeforeNextLine();
                    using (writer.DisableScope())
                    {
                        writer.EnsureSeparation(); // This is expected to do nothing since we're "part" of the disabled block
                        writer.WriteLine("DISABLED1");
                        writer.WriteLine("DISABLED2");
                    }
                    writer.WriteLine("LINE2");
                }
            );
        }

        [Fact]
        public void DisableScope_Message()
        {
            CodeWriterTest
            (
@"LINE1

#if 0 // MESSAGE
DISABLED1
DISABLED2
#endif
LINE2
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.DisableScope("MESSAGE"))
                    {
                        writer.WriteLine("DISABLED1");
                        writer.WriteLine("DISABLED2");
                    }
                    writer.WriteLine("LINE2");
                }
            );
        }

        [Fact]
        public void DisableScope_Indented()
        {
            CodeWriterTest
            (
@"LINE1
    LINE2

#if 0 // MESSAGE
    DISABLED1
    DISABLED2
#endif
    LINE3
LINE4
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.Indent())
                    {
                        writer.WriteLine("LINE2");
                        using (writer.DisableScope("MESSAGE"))
                        {
                            writer.WriteLine("DISABLED1");
                            writer.WriteLine("DISABLED2");
                        }
                        writer.WriteLine("LINE3");
                    }
                    writer.WriteLine("LINE4");
                }
            );
        }

        [Fact]
        public void DisableScope_NoDisable()
        {
            CodeWriterTest
            (
@"LINE1
DISABLED1
DISABLED2
LINE2
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.DisableScope(false, "MESSAGE"))
                    {
                        writer.WriteLine("DISABLED1");
                        writer.WriteLine("DISABLED2");
                    }
                    writer.WriteLine("LINE2");
                }
            );
        }

        [Fact]
        public void DisableScope_NoDisableEnsureSeparation()
        {
            CodeWriterTest
            (
@"LINE1

DISABLED1
DISABLED2
LINE2
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.DisableScope(false, "MESSAGE"))
                    {
                        writer.EnsureSeparation();
                        writer.WriteLine("DISABLED1");
                        writer.WriteLine("DISABLED2");
                    }
                    writer.WriteLine("LINE2");
                }
            );
        }
    }
}
