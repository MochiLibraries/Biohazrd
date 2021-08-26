using Biohazrd.Tests.Common;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Biohazrd.OutputGeneration.Tests
{
    public sealed class OutputSessionTests : BiohazrdTestBase
    {
        [Fact]
        public void AutoRenameConflictingFiles()
        {
            string file1;
            string file2;
            string file3;

            using (OutputSession outputSession = CreateOutputSession())
            {
                outputSession.AutoRenameConflictingFiles = true;

                file1 = Path.Combine(outputSession.BaseOutputDirectory, "Test.txt");
                file2 = Path.Combine(outputSession.BaseOutputDirectory, "Test_0.txt");
                file3 = Path.Combine(outputSession.BaseOutputDirectory, "Test_1.txt");

                Assert.False(File.Exists(file1));
                Assert.False(File.Exists(file2));
                Assert.False(File.Exists(file3));

                outputSession.Open<StreamWriter>("Test.txt").WriteLine("File1");
                outputSession.Open<StreamWriter>("Test.txt").WriteLine("File2");
                outputSession.Open<StreamWriter>("Test.txt").WriteLine("File3");
            }

            Assert.True(File.Exists(file1));
            Assert.True(File.Exists(file2));
            Assert.True(File.Exists(file3));

            Assert.Equal("File1", File.ReadAllText(file1).Trim());
            Assert.Equal("File2", File.ReadAllText(file2).Trim());
            Assert.Equal("File3", File.ReadAllText(file3).Trim());
        }

        [Fact]
        public void AutoRenameConflictingFiles_DifferentDirectoriesNoConflict()
        {
            string file1;
            string file2;

            using (OutputSession outputSession = CreateOutputSession())
            {
                outputSession.AutoRenameConflictingFiles = true;

                file1 = Path.Combine(outputSession.BaseOutputDirectory, "Test.txt");
                file2 = Path.Combine(outputSession.BaseOutputDirectory, "Subdirectory/Test.txt");

                Assert.False(File.Exists(file1));
                Assert.False(File.Exists(file2));

                outputSession.Open<StreamWriter>("Test.txt").WriteLine("File1");
                outputSession.Open<StreamWriter>("Subdirectory/Test.txt").WriteLine("File2");
            }

            Assert.True(File.Exists(file1));
            Assert.True(File.Exists(file2));

            Assert.Equal("File1", File.ReadAllText(file1).Trim());
            Assert.Equal("File2", File.ReadAllText(file2).Trim());
        }

        [Fact]
        public void AutoRenameConflictingFiles_ConflictWithDeduplicatedName()
        {
            string file1;
            string file2;
            string file3;

            using (OutputSession outputSession = CreateOutputSession())
            {
                outputSession.AutoRenameConflictingFiles = true;

                file1 = Path.Combine(outputSession.BaseOutputDirectory, "Test.txt");
                file2 = Path.Combine(outputSession.BaseOutputDirectory, "Test_0.txt");
                file3 = Path.Combine(outputSession.BaseOutputDirectory, "Test_1.txt");

                Assert.False(File.Exists(file1));
                Assert.False(File.Exists(file2));
                Assert.False(File.Exists(file3));

                outputSession.Open<StreamWriter>("Test.txt").WriteLine("File1");
                outputSession.Open<StreamWriter>("Test_0.txt").WriteLine("File2");
                outputSession.Open<StreamWriter>("Test.txt").WriteLine("File3");
            }

            Assert.True(File.Exists(file1));
            Assert.True(File.Exists(file2));
            Assert.True(File.Exists(file3));

            Assert.Equal("File1", File.ReadAllText(file1).Trim());
            Assert.Equal("File2", File.ReadAllText(file2).Trim());
            Assert.Equal("File3", File.ReadAllText(file3).Trim());
        }

        [Fact]
        public void AutoRenameConflictingFiles_Disabled()
        {
            string file1;
            string file2;

            using (OutputSession outputSession = CreateOutputSession())
            {
                outputSession.AutoRenameConflictingFiles = false;

                file1 = Path.Combine(outputSession.BaseOutputDirectory, "Test.txt");
                file2 = Path.Combine(outputSession.BaseOutputDirectory, "Test_0.txt");

                Assert.False(File.Exists(file1));
                Assert.False(File.Exists(file2));

                outputSession.Open<StreamWriter>("Test.txt").WriteLine("File1");
                Assert.Throws<InvalidOperationException>(() => outputSession.Open<StreamWriter>("Test.txt").WriteLine("File2"));
            }
        }

        [Fact]
        public void AutoRenameConflictingFiles_DirtyOutputDirectory()
        {
            string file1;
            string file2;
            string file3;
            string outputDirectory;

            using (OutputSession outputSession = CreateOutputSession())
            {
                outputSession.AutoRenameConflictingFiles = true;
                outputDirectory = outputSession.BaseOutputDirectory;

                file1 = Path.Combine(outputSession.BaseOutputDirectory, "Test.txt");
                file2 = Path.Combine(outputSession.BaseOutputDirectory, "Test_0.txt");
                file3 = Path.Combine(outputSession.BaseOutputDirectory, "Test_1.txt");

                Assert.False(File.Exists(file1));
                Assert.False(File.Exists(file2));
                Assert.False(File.Exists(file3));

                outputSession.Open<StreamWriter>("Test.txt").WriteLine("File1");
                outputSession.Open<StreamWriter>("Test.txt").WriteLine("File2");
                outputSession.Open<StreamWriter>("Test.txt").WriteLine("File3");
            }

            Assert.True(File.Exists(file1));
            Assert.True(File.Exists(file2));
            Assert.True(File.Exists(file3));

            Assert.Equal("File1", File.ReadAllText(file1).Trim());
            Assert.Equal("File2", File.ReadAllText(file2).Trim());
            Assert.Equal("File3", File.ReadAllText(file3).Trim());

            using (OutputSession outputSession = new())
            {
                outputSession.AutoRenameConflictingFiles = true;
                outputSession.BaseOutputDirectory = outputDirectory;

                outputSession.Open<StreamWriter>("Test.txt").WriteLine("NewFile1");
                outputSession.Open<StreamWriter>("Test.txt").WriteLine("NewFile2");
                outputSession.Open<StreamWriter>("Test.txt").WriteLine("NewFile3");
            }

            // Ensure the files were overwritten
            // (This ensures that the session didn't create Test_2, Test_3, and Test_4 files.)
            Assert.Equal("NewFile1", File.ReadAllText(file1).Trim());
            Assert.Equal("NewFile2", File.ReadAllText(file2).Trim());
            Assert.Equal("NewFile3", File.ReadAllText(file3).Trim());
        }

        [Fact]
        public void StaleOutputIsCleaned()
        {
            string fileA;
            string fileB;
            string outputDirectory;

            // Start a session and write two files into it
            using (OutputSession outputSession = CreateOutputSession())
            {
                outputDirectory = outputSession.BaseOutputDirectory;
                fileA = Path.Combine(outputSession.BaseOutputDirectory, "FileA.txt");
                fileB = Path.Combine(outputSession.BaseOutputDirectory, "FileB.txt");

                Assert.False(File.Exists(fileA));
                Assert.False(File.Exists(fileB));

                outputSession.Open<StreamWriter>("FileA.txt").WriteLine("A");
                outputSession.Open<StreamWriter>("FileB.txt").WriteLine("B");
            }

            Assert.True(File.Exists(fileA));
            Assert.True(File.Exists(fileB));
            Assert.Equal("A", File.ReadAllText(fileA).Trim());
            Assert.Equal("B", File.ReadAllText(fileB).Trim());

            // Start a new session and write only one of the files
            using (OutputSession outputSession = new())
            {
                outputSession.BaseOutputDirectory = outputDirectory;
                outputSession.Open<StreamWriter>("FileA.txt").WriteLine("NewA");
            }

            Assert.True(File.Exists(fileA));
            Assert.False(File.Exists(fileB));
            Assert.Equal("NewA", File.ReadAllText(fileA).Trim());
        }

        const string absurdlyLongName = "ThisFileNameIsSoLongThatItWillNotBeSavableOnMostFileSystemsBecauseEvenIfTheresNoPathLimitTheresStillAPath"
                + "ComponentLimitAccordingToAllKnownLawsOfAviationThereIsNoWayThisMemeIsStillRelevantICouldHaveJustGeneratedASuperLongStringButWheresThe"
                + "FunInThatThreeThousandTwoHundredAndTwentySixIsMyFavoriteNumber.txt";

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/180")]
        public void ExtremelyLongFileNamesAreTruncated()
        {
            Assert.True(absurdlyLongName.Length > 256);
            string writtenFilePath;

            using (OutputSession outputSession = CreateOutputSession())
            {
                outputSession.Open<StreamWriter>(absurdlyLongName).WriteLine("Test");
                Assert.Single(outputSession.FilesWritten);
                writtenFilePath = outputSession.FilesWritten.First();
            }

            Assert.True(File.Exists(writtenFilePath));
            Assert.Equal("Test", File.ReadAllText(writtenFilePath).Trim());
            Assert.StartsWith(Path.GetFileNameWithoutExtension(writtenFilePath), absurdlyLongName); // We don't hard-code any truncation logic, we just care that it got truncated.
            Assert.Equal(".txt", Path.GetExtension(writtenFilePath));
            Assert.True(Path.GetFileName(writtenFilePath).Length < 256);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/180")]
        public void ExtremelyLongFileNamesAreTruncatedAndDeduplicated()
        {
            Assert.True(absurdlyLongName.Length > 256);
            string file1;
            string file2;

            using (OutputSession outputSession = CreateOutputSession())
            {
                outputSession.AutoRenameConflictingFiles = true;
                outputSession.Open<StreamWriter>(absurdlyLongName).WriteLine("Test1");
                outputSession.Open<StreamWriter>(absurdlyLongName).WriteLine("Test2");

                Assert.Equal(2, outputSession.FilesWritten.Count);
                file1 = outputSession.FilesWritten.First();
                file2 = outputSession.FilesWritten.Last();
            }

            Assert.True(File.Exists(file1));
            Assert.True(File.Exists(file2));
            Assert.Equal("Test1", File.ReadAllText(file1).Trim());
            Assert.Equal("Test2", File.ReadAllText(file2).Trim());

            string noExtension1 = Path.GetFileNameWithoutExtension(file1);
            string noExtension2 = Path.GetFileNameWithoutExtension(file2);
            Assert.StartsWith(noExtension1, absurdlyLongName); // We don't hard-code any truncation logic, we just care that it got truncated.
            Assert.Equal($"{noExtension1}_0", noExtension2);

            Assert.Equal(".txt", Path.GetExtension(file1));
            Assert.Equal(".txt", Path.GetExtension(file2));
            Assert.True(Path.GetFileName(file1).Length < 256);
            Assert.True(Path.GetFileName(file2).Length < 256);
        }

        [Fact]
        [RelatedIssue("https://github.com/InfectedLibraries/Biohazrd/issues/212")]
        public void FileLogUsesForwardSlashes()
        {
            string fileLogPath;

            using (OutputSession outputSession = CreateOutputSession())
            {
                outputSession.Open<StreamWriter>(Path.Combine("Folder", "Test.txt")).WriteLine("Test");
                fileLogPath = Path.Combine(outputSession.BaseOutputDirectory, "FilesWritten.txt");
            }

            Assert.True(File.Exists(fileLogPath));
            string fileLog = File.ReadAllText(fileLogPath);
            Assert.DoesNotContain('\\', fileLog);
            Assert.Contains('/', fileLog);
        }
    }
}
