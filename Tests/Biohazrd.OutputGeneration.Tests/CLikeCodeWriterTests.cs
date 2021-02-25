using Xunit;

namespace Biohazrd.OutputGeneration.Tests
{
    public sealed class CLikeCodeWriterTests : BiohazrdCodeWriterTestBase<CLikeCodeWriter>
    {
        [Fact]
        public void Block()
        {
            CodeWriterTest
            (
@"if (SayHello)
{
    printf(""Hello, world!\n"");
}
",
                writer =>
                {
                    writer.WriteLine("if (SayHello)");
                    using (writer.Block())
                    {
                        writer.WriteLine(@"printf(""Hello, world!\n"");");
                    }
                }
            );
        }

        [Fact]
        public void HeaderComment1()
        {
            CodeWriterTest
            (
@"// GENERATED
CodeGoesHere
",
                session => session.GeneratedFileHeader = "GENERATED",
                writer => writer.WriteLine("CodeGoesHere")
            );
        }

        [Fact]
        public void HeaderComment2()
        {
            CodeWriterTest
            (
@"// GENERATED
// FILE
CodeGoesHere
",
                session => session.GeneratedFileHeader = "GENERATED\nFILE",
                writer => writer.WriteLine("CodeGoesHere")
            );
        }
    }
}
