using System.IO;
using System.Linq;
using Xunit;

namespace Biohazrd.Tests
{
    public sealed class FileAttributionTests
    {
        [Fact]
        public void IncludedNestedTypeIsAttributedCorrectly()
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
                Contents = @"
struct StructB
{
int FieldB;
};
",
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);

            TranslatedFile? fileA = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == "A.h");
            TranslatedFile? fileB = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == "B.h");
            Assert.NotNull(fileA);
            Assert.NotNull(fileB);
            Assert.True(fileA.WasInScope);
            Assert.True(fileB.WasInScope);

            Assert.Single(library.Declarations);
            Assert.Equal("StructA", library.Declarations[0].Name);
            TranslatedRecord structA = (TranslatedRecord)library.Declarations[0];
            Assert.Equal(fileA, structA.File);
            Assert.Equal(2, structA.TotalMemberCount);

            TranslatedDeclaration? fieldA = structA.Members.FirstOrDefault(m => m.Name == "FieldA");
            Assert.NotNull(fieldA);
            Assert.Equal(fileA, fieldA.File);

            TranslatedRecord? structB = structA.Members.OfType<TranslatedRecord>().FirstOrDefault();
            Assert.NotNull(structB);
            Assert.Equal("StructB", structB.Name);
            Assert.Equal(fileB, structB.File);
            Assert.Equal(1, structB.TotalMemberCount);
            TranslatedDeclaration fieldB = structB.Members[0];
            Assert.Equal("FieldB", fieldB.Name);
            Assert.Equal(fileB, fieldB.File);
        }

        [Fact(Skip = "https://github.com/InfectedLibraries/Biohazrd/issues/113")]
        public void IncludedFieldIsAttributedCorrectly()
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
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);

            TranslatedFile? fileA = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == "A.h");
            TranslatedFile? fileB = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == "B.h");
            Assert.NotNull(fileA);
            Assert.NotNull(fileB);
            Assert.True(fileA.WasInScope);
            Assert.True(fileB.WasInScope);

            Assert.Single(library.Declarations);
            Assert.Equal("StructA", library.Declarations[0].Name);
            TranslatedRecord structA = (TranslatedRecord)library.Declarations[0];
            Assert.Equal(fileA, structA.File);
            Assert.Equal(2, structA.TotalMemberCount);

            TranslatedDeclaration? fieldA = structA.Members.FirstOrDefault(m => m.Name == "FieldA");
            TranslatedDeclaration? fieldB = structA.Members.FirstOrDefault(m => m.Name == "FieldB");
            Assert.NotNull(fieldA);
            Assert.NotNull(fieldB);
            Assert.Equal(fileA, fieldA.File);
            Assert.Equal(fileB, fieldB.File);
        }

        [Fact(Skip = "https://github.com/InfectedLibraries/Biohazrd/issues/113")]
        public void IncludedEnumValuesAreAttributedCorrectly()
        {
            TranslatedLibraryBuilder builder = new();
            builder.AddFile(new SourceFile("A.h")
            {
                Contents = @"
enum class EnumA
{
ValueA,
#include ""B.h""
};
"
            });

            builder.AddFile(new SourceFile("B.h")
            {
                Contents = "ValueB,",
                IndexDirectly = false
            });

            TranslatedLibrary library = builder.Create();

            Assert.Empty(library.ParsingDiagnostics);

            TranslatedFile? fileA = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == "A.h");
            TranslatedFile? fileB = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == "B.h");
            Assert.NotNull(fileA);
            Assert.NotNull(fileB);
            Assert.True(fileA.WasInScope);
            Assert.True(fileB.WasInScope);

            Assert.Single(library.Declarations);
            Assert.Equal("EnumA", library.Declarations[0].Name);
            TranslatedEnum enumA = (TranslatedEnum)library.Declarations[0];
            Assert.Equal(fileA, enumA.File);
            Assert.Equal(2, enumA.Values.Count);

            TranslatedEnumConstant? valueA = enumA.Values.FirstOrDefault(m => m.Name == "ValueA");
            TranslatedEnumConstant? valueB = enumA.Values.FirstOrDefault(m => m.Name == "ValueB");
            Assert.NotNull(valueA);
            Assert.NotNull(valueB);
            Assert.Equal(fileA, valueA.File);
            Assert.Equal(fileB, valueB.File);
        }
    }
}
