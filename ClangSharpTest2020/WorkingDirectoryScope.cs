using System;
using System.IO;

namespace ClangSharpTest2020
{
    internal readonly struct WorkingDirectoryScope : IDisposable
    {
        private readonly string OldWorkingDirectory;

        public WorkingDirectoryScope(string newWorkingDirectory, bool createIfMissing = true)
        {
            OldWorkingDirectory = Environment.CurrentDirectory;

            if (!Path.IsPathRooted(newWorkingDirectory))
            { newWorkingDirectory = Path.Combine(OldWorkingDirectory, newWorkingDirectory); }

            if (createIfMissing)
            { Directory.CreateDirectory(newWorkingDirectory); }

            Environment.CurrentDirectory = newWorkingDirectory;
        }

        void IDisposable.Dispose()
        {
            if (OldWorkingDirectory != null)
            { Environment.CurrentDirectory = OldWorkingDirectory; }
        }
    }
}
