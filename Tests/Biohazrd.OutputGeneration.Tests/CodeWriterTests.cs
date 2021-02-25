using System;
using System.IO;
using Xunit;

namespace Biohazrd.OutputGeneration.Tests
{
    public sealed class CodeWriterTests : BiohazrdCodeWriterTestBase<CodeWriterTests.DummyCodeWriter>
    {
        /// <summary>Barenones code writer implementation used for testing.</summary>
        [ProvidesOutputSessionFactory]
        public sealed class DummyCodeWriter : CodeWriter
        {
            public string? BetweenHeaderAndCode { get; set; } = null;

            private DummyCodeWriter(OutputSession session, string filePath)
                : base(session, filePath)
            { }

            private static OutputSession.WriterFactory<DummyCodeWriter> FactoryMethod => (session, filePath) => new DummyCodeWriter(session, filePath);

            protected override void WriteOutHeaderComment(StreamWriter writer)
                => OutputSession.WriteHeader(writer, "HEADER: ");

            protected override void WriteBetweenHeaderAndCode(StreamWriter writer)
            {
                if (BetweenHeaderAndCode is not null)
                { writer.WriteLine(BetweenHeaderAndCode); }
            }

            // Test wrappers to expose protected helpers
            public new IndentScope CreateIndentScope(string? startLine, string? endLine)
                => base.CreateIndentScope(startLine, endLine);

            public new LeftAdjustedScope CreateLeftAdjustedScope(string startLine, string endLine)
                => base.CreateLeftAdjustedScope(startLine, endLine);
        }

        [Fact]
        public void HelloWorld1()
        {
            CodeWriterTest
            (
                "Hello, world!\n",
                writer => writer.WriteLine("Hello, world!")
            );
        }

        [Fact]
        public void HelloWorld2()
        {
            CodeWriterTest
            (
                "Hello, world!",
                writer => writer.Write("Hello, world!")
            );
        }

        [Fact]
        public void HelloWorld3()
        {
            CodeWriterTest
            (
                "Hello, world!",
                writer =>
                {
                    foreach (char c in "Hello, world!")
                    { writer.Write(c); }
                }
            );
        }

        [Fact]
        public void GeneratedHeader1()
        {
            CodeWriterTest
            (
                "HEADER: GENERATED\nHello, world!\n",
                outputSession => outputSession.GeneratedFileHeader = "GENERATED",
                writer => writer.WriteLine("Hello, world!")
            );
        }

        [Fact]
        public void GeneratedHeader2()
        {
            CodeWriterTest
            (
                "HEADER: GENERATED\nHEADER: FILE\nHello, world!\n",
                outputSession => outputSession.GeneratedFileHeader = "GENERATED\nFILE",
                writer => writer.WriteLine("Hello, world!")
            );
        }

        [Fact]
        public void BetweenHeaderAndCode()
        {
            CodeWriterTest
            (
                "HEADER: GENERATED\nBETWEEN\nHello, world!\n",
                outputSession => outputSession.GeneratedFileHeader = "GENERATED",
                writer =>
                {
                    writer.BetweenHeaderAndCode = "BETWEEN";
                    writer.WriteLine("Hello, world!");
                }
            );
        }

        [Fact]
        public void BetweenHeaderAndCode_NoHeader()
        {
            CodeWriterTest
            (
                "BETWEEN\nHello, world!\n",
                writer =>
                {
                    writer.BetweenHeaderAndCode = "BETWEEN";
                    writer.WriteLine("Hello, world!");
                }
            );
        }

        [Fact]
        public void ExplicitFinishDoesNotAllowEarlyRead()
        {
            FillCodeWriterAndGetCode
            (
                (writer, path) =>
                {
                    writer.WriteLine("Hello, world!");
                    writer.Finish();
                    // When the writer is explicitly finished, the file should still be locked
                    Assert.Throws<IOException>(() => File.ReadAllText(path));
                }
            );
        }

        [Fact]
        public void ExplicitDisposeAllowsEarlyRead()
        {
            string? code2 = null;
            string code = FillCodeWriterAndGetCode
            (
                (writer, path) =>
                {
                    writer.WriteLine("Hello, world!");
                    writer.Dispose();
                    // When the writer is explicitly disposed, we should be able to read the file before the output session ends
                    code2 = File.ReadAllText(path);
                }
            );
            Assert.Equal("Hello, world!\n", code, ignoreLineEndingDifferences: true);
            Assert.Equal("Hello, world!\n", code2, ignoreLineEndingDifferences: true);
        }

        [Fact]
        public void DoubleExplicitFinishNotAllowed()
        {
            FillCodeWriterAndGetCode
            (
                (writer, path) =>
                {
                    writer.WriteLine("Hello, world!");
                    writer.Finish();
                    // Finishing more than once is forbidden
                    Assert.Throws<InvalidOperationException>(() => writer.Finish());
                }
            );
        }

        [Fact]
        public void ExplicitFinishAndExplicitDisposeIsok()
        {
            // This test simply just makes sure this doesn't throw an exception
            FillCodeWriterAndGetCode
            (
                writer =>
                {
                    writer.WriteLine("Hello, world!");
                    writer.Finish();
                    writer.Dispose();
                }
            );
        }

        [Fact]
        public void Indent1()
        {
            CodeWriterTest
            (
@"LINE1
    LINE2
LINE3
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.Indent())
                    { writer.WriteLine("LINE2"); }
                    writer.WriteLine("LINE3");
                }
            );
        }

        [Fact]
        public void Indent2()
        {
            CodeWriterTest
            (
@"LINE1
    LINE2
    LINE3
LINE4
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.Indent())
                    {
                        writer.WriteLine("LINE2");
                        writer.WriteLine("LINE3");
                    }
                    writer.WriteLine("LINE4");
                }
            );
        }

        [Fact]
        public void Indent3()
        {
            CodeWriterTest
            (
@"LINE1
    LINE2
    LINE3
LINE4
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.Indent())
                    { writer.WriteLine("LINE2\nLINE3"); }
                    writer.WriteLine("LINE4");
                }
            );
        }

        [Fact]
        public void Indent4()
        {
            CodeWriterTest
            (
                // Note the lack of spaces on line 3, which is empty
                "LINE1\n    LINE2\n\n    LINE4\nLINE5\n",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.Indent())
                    {
                        writer.WriteLine("LINE2");
                        writer.WriteLine();
                        writer.WriteLine("LINE4");
                    }
                    writer.WriteLine("LINE5");
                }
            );
        }

        [Fact]
        public void NestedIndent()
        {
            CodeWriterTest
            (
@"LINE1
    LINE2
        LINE3
    LINE4
LINE5
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.Indent())
                    {
                        writer.WriteLine("LINE2");
                        using (writer.Indent())
                        {
                            writer.WriteLine("LINE3");
                        }
                        writer.WriteLine("LINE4");
                    }
                    writer.WriteLine("LINE5");
                }
            );
        }

        [Fact]
        public void CustomIndentScope()
        {
            CodeWriterTest
            (
@"LINE1
START
    LINE2

    LINE3
END
LINE4
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.CreateIndentScope("START", "END"))
                    {
                        writer.WriteLine("LINE2");
                        writer.WriteLine();
                        writer.WriteLine("LINE3");
                    }
                    writer.WriteLine("LINE4");
                }
            );
        }

        [Fact]
        public void CustomIndentScope_NoStart()
        {
            CodeWriterTest
            (
@"LINE1
    LINE2

    LINE3
END
LINE4
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.CreateIndentScope(null, "END"))
                    {
                        writer.WriteLine("LINE2");
                        writer.WriteLine();
                        writer.WriteLine("LINE3");
                    }
                    writer.WriteLine("LINE4");
                }
            );
        }

        [Fact]
        public void CustomIndentScope_NoEnd()
        {
            CodeWriterTest
            (
@"LINE1
START
    LINE2

    LINE3
LINE4
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.CreateIndentScope("START", null))
                    {
                        writer.WriteLine("LINE2");
                        writer.WriteLine();
                        writer.WriteLine("LINE3");
                    }
                    writer.WriteLine("LINE4");
                }
            );
        }

        [Fact]
        public void CustomIndentScope_NoStartNoEnd()
        {
            CodeWriterTest
            (
@"LINE1
    LINE2

    LINE3
LINE4
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.CreateIndentScope(null, null))
                    {
                        writer.WriteLine("LINE2");
                        writer.WriteLine();
                        writer.WriteLine("LINE3");
                    }
                    writer.WriteLine("LINE4");
                }
            );
        }

        [Fact]
        public void CustomIndentScope_Nested()
        {
            CodeWriterTest
            (
@"LINE1
START
    LINE2
    START_NESTED
        NESTED1

        NESTED2
    END_NESTED
    LINE3
END
LINE4
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.CreateIndentScope("START", "END"))
                    {
                        writer.WriteLine("LINE2");
                        using (writer.CreateIndentScope("START_NESTED", "END_NESTED"))
                        {
                            writer.WriteLine("NESTED1");
                            writer.WriteLine();
                            writer.WriteLine("NESTED2");
                        }
                        writer.WriteLine("LINE3");
                    }
                    writer.WriteLine("LINE4");
                }
            );
        }

        [Fact]
        public void SingleLineIndent()
        {
            CodeWriterTest
            (
@"LINE1
    LINE2
LINE3
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    writer.WriteLineIndented("LINE2");
                    writer.WriteLine("LINE3");
                }
            );
        }

        [Fact]
        public void SingleLineIndent_Nested()
        {
            CodeWriterTest
            (
@"LINE1
    LINE2
        LINE3
    LINE4
LINE5
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.Indent())
                    {
                        writer.WriteLine("LINE2");
                        writer.WriteLineIndented("LINE3");
                        writer.WriteLine("LINE4");
                    }
                    writer.WriteLine("LINE5");
                }
            );
        }

        [Fact]
        public void LeftAdjustedLine1()
        {
            CodeWriterTest
            (
@"LINE1
    LINE2
LINE3
    LINE4
LINE5
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.Indent())
                    {
                        writer.WriteLine("LINE2");
                        writer.WriteLineLeftAdjusted("LINE3");
                        writer.WriteLine("LINE4");
                    }
                    writer.WriteLine("LINE5");
                }
            );
        }

        [Fact]
        public void LeftAdjustedLine2()
        {
            CodeWriterTest
            (
@"LINE1
    LINE2
        LINE3
LINE4
        LINE5
    LINE6
LINE7
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.Indent())
                    {
                        writer.WriteLine("LINE2");
                        using (writer.Indent())
                        {
                            writer.WriteLine("LINE3");
                            writer.WriteLineLeftAdjusted("LINE4");
                            writer.WriteLine("LINE5");
                        }
                        writer.WriteLine("LINE6");
                    }
                    writer.WriteLine("LINE7");
                }
            );
        }

        [Fact]
        public void LeftAdjustedScope()
        {
            CodeWriterTest
(
@"LINE1
    LINE2
START
    LINE3
END
    LINE4
LINE5
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.Indent())
                    {
                        writer.WriteLine("LINE2");
                        using (writer.CreateLeftAdjustedScope("START", "END"))
                        {
                            writer.WriteLine("LINE3");
                        }
                        writer.WriteLine("LINE4");
                    }
                    writer.WriteLine("LINE5");
                }
            );
        }

        [Fact]
        public void PrefixScope()
        {
            CodeWriterTest
            (
@"LINE1
PREFIX LINE2
PREFIX LINE3
LINE4
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    using (writer.Prefix("PREFIX "))
                    {
                        writer.WriteLine("LINE2");
                        writer.WriteLine("LINE3");
                    }
                    writer.WriteLine("LINE4");
                }
            );
        }

        [Fact]
        public void EnsureSeparation()
        {
            CodeWriterTest
            (
@"LINE1

LINE2
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    writer.EnsureSeparation();
                    writer.WriteLine("LINE2");
                }
            );
        }

        [Fact]
        public void EnsureSeparation_NoSeparationNeeded()
        {
            CodeWriterTest
            (
@"LINE1
LINE2
",
                writer =>
                {
                    writer.WriteLine("LINE1");
                    writer.NoSeparationNeededBeforeNextLine();
                    writer.EnsureSeparation();
                    writer.WriteLine("LINE2");
                }
            );
        }

        [Fact]
        public void EnsureSeparation_HeaderNoSeparate()
        {
            CodeWriterTest
            (
@"HEADER: GENERATED_HEADER
LINE
",
                session => session.GeneratedFileHeader = "GENERATED_HEADER",
                writer =>
                {
                    writer.EnsureSeparation();
                    writer.WriteLine("LINE");
                }
            );
        }
    }
}
