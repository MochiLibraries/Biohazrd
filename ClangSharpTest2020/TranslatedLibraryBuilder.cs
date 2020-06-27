using System.Collections.Generic;
using System.IO;

namespace ClangSharpTest2020
{
    public sealed class TranslatedLibraryBuilder
    {
        private readonly List<string> CommandLineArguments = new List<string>();
        private readonly List<string> FilePaths = new List<string>();

        public void AddFile(string filePath)
        {
            if (!File.Exists(filePath))
            { throw new FileNotFoundException("The specified file does not exist.", filePath); }

            // Ensure the path is absolute
            // (That way if the working directory changes, we still have a valid path.)
            // (This also normalizes the path.)
            filePath = Path.GetFullPath(filePath);

            FilePaths.Add(filePath);
        }

        public void AddFiles(IEnumerable<string> filePaths)
        {
            foreach (string filePath in filePaths)
            { AddFile(filePath); }
        }

        public void AddFiles(params string[] filePaths)
            => AddFiles((IEnumerable<string>)filePaths);

        public void AddCommandLineArgument(string commandLineArgument)
            => CommandLineArguments.Add(commandLineArgument);

        public void AddCommandLineArguments(IEnumerable<string> commandLineArguments)
            => CommandLineArguments.AddRange(commandLineArguments);

        public void AddCommandLineArguments(params string[] commandLineArguments)
            => AddCommandLineArguments((IEnumerable<string>)commandLineArguments);

        public TranslatedLibrary Create()
            => new TranslatedLibrary(CommandLineArguments, FilePaths);
    }
}
