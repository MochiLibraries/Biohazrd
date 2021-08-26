using Biohazrd.Tests.Common;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class BasicTests : BiohazrdTestBase
    {
        private void SmokeTestAssert(TranslatedLibrary library, string? fileName, string structName, string fieldName)
        {
            Assert.Empty(library.ParsingDiagnostics);

            // There should only be one translated file because we only provided a single input
            if (fileName is not null)
            {
                Assert.Single(library.Files);
                Assert.Equal(fileName, Path.GetFileName(library.Files[0].FilePath));
                Assert.NotEqual(IntPtr.Zero, library.Files[0].Handle);
            }

            // There should be a single top-level struct declaration named `structName`
            Assert.Single(library.Declarations);
            Assert.IsType<TranslatedRecord>(library.Declarations[0]);

            TranslatedRecord record = (TranslatedRecord)library.Declarations[0];
            Assert.Equal(RecordKind.Struct, record.Kind);
            Assert.Equal(structName, record.Name);

            // There should be a single public field named `fieldName` at offset 0
            Assert.Single(record.Members);
            Assert.IsType<TranslatedNormalField>(record.Members[0]);

            TranslatedNormalField field = (TranslatedNormalField)record.Members[0];
            Assert.Equal(fieldName, field.Name);
            Assert.Equal(0, field.Offset);
            Assert.Equal(0, field.Offset);
        }

        [Fact]
        public void BiohazrdCanTranslateFileOnDisk()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile("BasicTests.h");
            TranslatedLibrary library = builder.Create();
            SmokeTestAssert(library, "BasicTests.h", "HelloWorld", "ItWorks");
        }

        [Fact]
        public void BiohazrdCanTranslateAFileInMemory()
        {
            string filePath = OperatingSystem.IsWindows() ? @"C:\<>\<>ThisFileDoesntExist.h" : @"/<>/<>ThisFileDoesntExist.h";
            Assert.True(Path.IsPathRooted(filePath), "This test must use an absolute path.");
            Assert.False(File.Exists(filePath), "The file must not actually exist on disk.");

            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile(filePath)
            {
                Contents = @"
struct GoodbyeWorld
{
    int ThisWorksToo;
};
"
            });
            TranslatedLibrary library = builder.Create();
            SmokeTestAssert(library, "<>ThisFileDoesntExist.h", "GoodbyeWorld", "ThisWorksToo");
        }

        [Fact]
        public void BiohazrdCanTranslateAFileInMemoryWithRelativePath()
        {
            string fileName = @"<>ThisFileDoesntExist.h";
            Assert.False(Path.IsPathRooted(fileName), "This test must use a relative path.");
            Assert.False(File.Exists(fileName), "The file must not actually exist on disk.");

            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile(fileName)
            {
                Contents = @"
struct GoodbyeWorld
{
    int ThisWorksToo;
};
"
            });
            TranslatedLibrary library = builder.Create();
            SmokeTestAssert(library, fileName, "GoodbyeWorld", "ThisWorksToo");
        }

        [Fact]
        public void BiohazrdCanOverrideTheContentsOfAFile()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile(Path.GetFullPath("BasicTests.h"))
            {
                Contents = @"
struct GoodbyeWorld
{
    int ThisWorksToo;
};
"
            });
            TranslatedLibrary library = builder.Create();
            SmokeTestAssert(library, "BasicTests.h", "GoodbyeWorld", "ThisWorksToo");
        }

        [Fact]
        public void BiohazrdCanOverrideTheContentsOfAnIncludedFile()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile(Path.GetFullPath("BasicTests.h"))
            {
                Contents = @"
struct GoodbyeWorld
{
    int ThisWorksToo;
};
",
                IndexDirectly = false
            });
            builder.AddFile("BasicTests_IncludesBasicTests.h");
            TranslatedLibrary library = builder.Create();
            SmokeTestAssert(library, fileName: null, "GoodbyeWorld", "ThisWorksToo");
        }

        [Fact]
        public void FileNotIndexedDoesNotGetTranslated()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = "struct StructA {};"
            });
            builder.AddFile(new SourceFile("B.h")
            {
                Contents = "struct StructB {};",
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);

            Assert.Single(library.Declarations);
            Assert.Equal("StructA", library.Declarations[0].Name);

            // Both files should still be present, but B.h shouldn't have a Clang handle since it never appeared
            Assert.Equal(2, library.Files.Length);
            TranslatedFile? fileA = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == "A.h");
            TranslatedFile? fileB = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == "B.h");
            Assert.NotNull(fileA);
            Assert.NotNull(fileB);
            Assert.NotEqual(IntPtr.Zero, fileA.Handle);
            Assert.Equal(IntPtr.Zero, fileB.Handle);
        }

        [Fact]
        public void FileWithNoDeclarationsIsStillTranslated()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = "struct StructA {};"
            });
            builder.AddFile(new SourceFile("B.h")
            {
                Contents = @"
#if 0 // This file is effectively empty since it has no declarations.
struct StructB {};
#endif
",
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);

            Assert.Single(library.Declarations);
            Assert.Equal("StructA", library.Declarations[0].Name);

            // Both files should still be present
            // We do not check the handle of B.h since it's an implementation detail whether or not it might get one.
            // (It comes down to whether Clang attributes any cursors to it, which it might if preprocessing cursors are enabled.)
            Assert.Equal(2, library.Files.Length);
            TranslatedFile? fileA = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == "A.h");
            TranslatedFile? fileB = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == "B.h");
            Assert.NotNull(fileA);
            Assert.NotNull(fileB);
            Assert.NotEqual(IntPtr.Zero, fileA.Handle);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/132")]
        public void EmptyIndexFileDoesNotCrash()
        {
            TranslatedLibraryBuilder builder = new();
            TranslatedLibrary library = builder.Create();
            Assert.NotNull(library);
            Assert.Empty(library.Declarations);
        }

        [Fact]
        public void InputFileOrderIsRetained()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("C.h")
            {
                Contents = @"
#pragma once
#include ""A.h""
struct C { };
"
            });
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
#pragma once
#include ""B.h""
struct A { };
"
            });
            builder.AddFile(new SourceFile("B.h")
            {
                Contents = @"
#pragma once
#include ""B.h""
struct B { };
"
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);
            Assert.Equal(3, library.Files.Length);
            Assert.Equal("C.h", Path.GetFileName(library.Files[0].FilePath));
            Assert.Equal("A.h", Path.GetFileName(library.Files[1].FilePath));
            Assert.Equal("B.h", Path.GetFileName(library.Files[2].FilePath));
        }

        [Fact]
        public void TranslateEvenWithParsingErrorsFalse()
        {
            TranslatedLibrary library = CreateLibraryBuilder("struct BrokenStruct { int x; }", options: new() { TranslateEvenWithParsingErrors = false }).Create();
            Assert.Contains(library.ParsingDiagnostics, d => d.IsError && d.IsFromClang);
            Assert.Empty(library.Declarations);
        }

        [Fact]
        public void TranslateEvenWithParsingErrorsTrue()
        {
            TranslatedLibrary library = CreateLibraryBuilder("struct BrokenStruct { int x; }", options: new() { TranslateEvenWithParsingErrors = true }).Create();
            Assert.Contains(library.ParsingDiagnostics, d => d.IsError && d.IsFromClang);
            Assert.NotEmpty(library.Declarations);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/201")]
        public void ClangFindsSystemIncludes()
        {
            // On Windows stddef.h will be found from the UCRT.
            // On Linux it needs to come from the Clang resource directory.
            TranslatedLibrary library = CreateLibrary
            (
                @"
#include <stddef.h>
size_t Test();
"
            );
            library.FindDeclaration<TranslatedFunction>("Test");
        }
    }
}
