using System.IO;
using System.Linq;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class ScopeTests
    {
        [Fact]
        public void OutOfScopeDeclarationsDoNotAppear()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
#include ""OutOfScopeDeclarationsDoNotAppear.h""

struct InScopeStruct {};
"
            });

            File.WriteAllText("OutOfScopeDeclarationsDoNotAppear.h", "struct OutOfScopeStruct {};");

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);
            Assert.Single(library.Declarations);
            Assert.Equal("InScopeStruct", library.Declarations[0].Name);
        }

        [Fact]
        public void OutOfScopeDeclarationsDoNotAppear2()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
#include ""B.h""

struct InScopeStruct {};
"
            });

            builder.AddFile(new SourceFile("B.h")
            {
                Contents = "struct OutOfScopeStruct {};",
                IsInScope = false,
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);
            Assert.Single(library.Declarations);
            Assert.Equal("InScopeStruct", library.Declarations[0].Name);
        }

        [Fact]
        public void UnindexedInScopeFileIsTranslated()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
#include ""B.h""

struct StructA {};
"
            });

            builder.AddFile(new SourceFile("B.h")
            {
                Contents = "struct StructB {};",
                IsInScope = true,
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);
            Assert.Equal(2, library.Declarations.Count);
            Assert.Contains("StructB", library.Declarations.Select(d => d.Name));
            Assert.Contains("StructA", library.Declarations.Select(d => d.Name));
        }

        [Fact]
        public void UnindexedInScopeFileIncludedByAnotherUnindexedInScopeFileIsTranslated()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
#include ""B.h""

struct StructA {};
"
            });

            builder.AddFile(new SourceFile("B.h")
            {
                Contents = @"#include ""C.h""",
                IsInScope = true,
                IndexDirectly = false
            });

            builder.AddFile(new SourceFile("C.h")
            {
                Contents = "struct StructB {};",
                IsInScope = true,
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);
            Assert.Equal(2, library.Declarations.Count);
            Assert.Contains("StructB", library.Declarations.Select(d => d.Name));
            Assert.Contains("StructA", library.Declarations.Select(d => d.Name));
        }

        [Fact]
        public void UnindexedInScopeFileIncludedByAnotherUnindexedOutOfScopeFileIsTranslated()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
#include ""B.h""

struct StructA {};
"
            });

            builder.AddFile(new SourceFile("B.h")
            {
                Contents = @"#include ""C.h""",
                IsInScope = false,
                IndexDirectly = false
            });

            builder.AddFile(new SourceFile("C.h")
            {
                Contents = "struct StructB {};",
                IsInScope = true,
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);
            Assert.Equal(2, library.Declarations.Count);
            Assert.Contains("StructB", library.Declarations.Select(d => d.Name));
            Assert.Contains("StructA", library.Declarations.Select(d => d.Name));
        }

        [Fact]
        public void UnindexedInScopeFileNotIncludedIsNotTranslated()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"struct StructA {};"
            });

            builder.AddFile(new SourceFile("B.h")
            {
                Contents = "struct StructB {};",
                IsInScope = true,
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);
            Assert.Single(library.Declarations);
            Assert.Equal("StructA", library.Declarations[0].Name);
        }

        [Fact]
        public void OutOfScopeDeclarationInsideInScopeDeclarationIsPromoted()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
struct StructA
{
int FieldA;
#include ""B.h""
};
"
            });

            builder.AddFile(new SourceFile("B.h")
            {
                Contents = "int FieldB;",
                IsInScope = false,
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);
            Assert.Single(library.Declarations);
            Assert.Equal("StructA", library.Declarations[0].Name);
            TranslatedRecord record = (TranslatedRecord)library.Declarations[0];
            Assert.Equal(2, record.TotalMemberCount);
            Assert.Contains("FieldA", record.Members.Select(m => m.Name));
            Assert.Contains("FieldB", record.Members.Select(m => m.Name));

            // Ensure the file ended was promoted into the library's file list
#if false // Blocked by https://github.com/InfectedLibraries/Biohazrd/issues/113
            TranslatedFile? fileB = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == "B.h");
            Assert.NotNull(fileB);
            Assert.False(fileB.WasInScope);
#endif
        }

        [Fact]
        public void OutOfScopeDeclarationInsideInScopeDeclarationIsPromoted2()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
struct StructA
{
int FieldA;
# include ""B.h""
};
"
            });

            builder.AddFile(new SourceFile("B.h")
            {
                Contents = @"
struct StructB
{
int FieldB;
};
",
                IsInScope = false,
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Equal(0, library.ParsingDiagnostics.Count(d => d.Severity > Severity.Warning)); // There must be no (fatal) errors
            Assert.Equal(1, library.ParsingDiagnostics.Count(d => d.Severity == Severity.Warning)); // There should be a single warning about B.h getting promoted
            Assert.Single(library.Declarations);
            Assert.Equal("StructA", library.Declarations[0].Name);
            TranslatedRecord record = (TranslatedRecord)library.Declarations[0];
            Assert.Equal(2, record.TotalMemberCount);
            Assert.Contains("FieldA", record.Members.Select(m => m.Name));

            TranslatedRecord? structB = record.Members.OfType<TranslatedRecord>().FirstOrDefault();
            Assert.NotNull(structB);
            Assert.Equal("StructB", structB.Name);
            Assert.Equal(1, structB.TotalMemberCount);
            Assert.Contains("FieldB", structB.Members.Select(m => m.Name));

            // Ensure the file ended was promoted into the library's file list
            TranslatedFile? fileB = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == "B.h");
            Assert.NotNull(fileB);
            Assert.False(fileB.WasInScope);
        }

        [Fact]
        public void OutOfScopeDeclarationInsideInScopeExternCDeclarationIsNotInScope()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
extern ""C""
{
int FieldA;
# include ""B.h""
};
"
            });

            builder.AddFile(new SourceFile("B.h")
            {
                Contents = "int FieldB;",
                IsInScope = false,
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);
            Assert.Single(library.Files);
            Assert.Single(library.Declarations);
            Assert.Equal("FieldA", library.Declarations[0].Name);
        }

        [Fact]
        public void InScopeDeclarationInsideOutOfScopeExternCDeclarationIsInScope()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
extern ""C""
{
int FieldA;
# include ""B.h""
};
",
                IsInScope = false
            });

            builder.AddFile(new SourceFile("B.h")
            {
                Contents = "int FieldB;",
                IsInScope = true,
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);
            Assert.Single(library.Files);
            Assert.Single(library.Declarations);
            Assert.Equal("FieldB", library.Declarations[0].Name);
        }

        [Fact]
        public void BiohazrdSkipsSystemHeaders()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
#pragma clang system_header
struct StructA {};
"
            });

            builder.AddFile(new SourceFile("B.h")
            {
                Contents = @"struct StructB {};"
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);
            Assert.Equal(2, library.Files.Length);
            Assert.Single(library.Declarations);
            Assert.Equal("StructB", library.Declarations[0].Name);
        }

        [Fact]
        public void BiohazrdDoesNotSkipSystemHeadersWhenToldNotTo()
        {
            TranslatedLibraryBuilder builder = new()
            {
                Options = new TranslationOptions()
                {
                    SystemHeadersAreAlwaysOutOfScope = false
                }
            };

            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
#pragma clang system_header
struct StructA {};
"
            });

            builder.AddFile(new SourceFile("B.h")
            {
                Contents = @"struct StructB {};"
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);
            Assert.Equal(2, library.Files.Length);
            Assert.Equal(2, library.Declarations.Count);

            Assert.Contains("StructA", library.Declarations.Select(d => d.Name));
            Assert.Contains("StructB", library.Declarations.Select(d => d.Name));
        }
    }
}
