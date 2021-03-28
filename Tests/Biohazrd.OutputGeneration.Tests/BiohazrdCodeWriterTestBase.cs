using Biohazrd.Tests.Common;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Biohazrd.OutputGeneration.Tests
{
    public abstract partial class BiohazrdCodeWriterTestBase<TCodeWriter> : BiohazrdTestBase
        where TCodeWriter : CodeWriter
    {
        protected string FillCodeWriterAndGetCode(string fileName, Action<OutputSession>? customizeOutputSession, Action<TCodeWriter, string> outputBuilder, [CallerMemberName] string testName = null!)
        {
            // Ideally we should create the code writer in a way that doesn't actually require writing its contents to the disk, but things aren't currently architected to enable that.
            string fullPath;
            using (OutputSession outputSession = CreateOutputSession(testName))
            {
                customizeOutputSession?.Invoke(outputSession);
                fullPath = Path.Combine(outputSession.BaseOutputDirectory, fileName);
                TCodeWriter writer = outputSession.Open<TCodeWriter>(fileName);
                outputBuilder(writer, fullPath);
            }

            return File.ReadAllText(fullPath);
        }

        protected string FillCodeWriterAndGetCode(string fileName, Action<TCodeWriter, string> outputBuilder, [CallerMemberName] string testName = null!)
            => FillCodeWriterAndGetCode(fileName, null, outputBuilder, testName);

        protected string FillCodeWriterAndGetCode(Action<OutputSession>? customizeOutputSession, Action<TCodeWriter, string> outputBuilder, [CallerMemberName] string testName = null!)
            => FillCodeWriterAndGetCode($"{testName}.txt", customizeOutputSession, outputBuilder, testName);

        protected string FillCodeWriterAndGetCode(Action<TCodeWriter, string> outputBuilder, [CallerMemberName] string testName = null!)
            => FillCodeWriterAndGetCode((Action<OutputSession>?)null, outputBuilder, testName);

        //-----------------------------------------------------------------------------------------
        // More typical overloads for outputBuilder without path parameter
        //-----------------------------------------------------------------------------------------
        protected string FillCodeWriterAndGetCode(string fileName, Action<OutputSession>? customizeOutputSession, Action<TCodeWriter> outputBuilder, [CallerMemberName] string testName = null!)
            => FillCodeWriterAndGetCode(fileName, customizeOutputSession, (w, p) => outputBuilder(w), testName);

        protected string FillCodeWriterAndGetCode(string fileName, Action<TCodeWriter> outputBuilder, [CallerMemberName] string testName = null!)
            => FillCodeWriterAndGetCode(fileName, (w, p) => outputBuilder(w), testName);

        protected string FillCodeWriterAndGetCode(Action<OutputSession>? customizeOutputSession, Action<TCodeWriter> outputBuilder, [CallerMemberName] string testName = null!)
            => FillCodeWriterAndGetCode(customizeOutputSession, (w, p) => outputBuilder(w), testName);

        protected string FillCodeWriterAndGetCode(Action<TCodeWriter> outputBuilder, [CallerMemberName] string testName = null!)
            => FillCodeWriterAndGetCode((w, p) => outputBuilder(w), testName);

        protected void CodeWriterTest(string expectedCode, Action<OutputSession>? customizeOutputSession, Action<TCodeWriter, string> outputBuilder, [CallerMemberName] string testName = null!)
        {
            string actualCode = FillCodeWriterAndGetCode(customizeOutputSession, outputBuilder, testName);
            Assert.Equal(expectedCode, actualCode, ignoreLineEndingDifferences: true);
        }

        protected void CodeWriterTest(string expectedCode, Action<OutputSession>? customizeOutputSession, Action<TCodeWriter> outputBuilder, [CallerMemberName] string testName = null!)
            => CodeWriterTest(expectedCode, customizeOutputSession, (w, p) => outputBuilder(w), testName);

        protected void CodeWriterTest(string expectedCode, Action<TCodeWriter, string> outputBuilder, [CallerMemberName] string testName = null!)
            => CodeWriterTest(expectedCode, null, outputBuilder, testName);

        protected void CodeWriterTest(string expectedCode, Action<TCodeWriter> outputBuilder, [CallerMemberName] string testName = null!)
            => CodeWriterTest(expectedCode, null, (w, p) => outputBuilder(w), testName);
    }
}
