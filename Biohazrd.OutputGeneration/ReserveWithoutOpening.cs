using System;
using System.IO;

namespace Biohazrd.OutputGeneration
{
    internal sealed class ReserveWithoutOpening : IDisposable
    {
        public string OutputPath { get; }
        private FileStream? LockingStream = null;

        public ReserveWithoutOpening(string outputPath)
            => OutputPath = outputPath;

        public void LockFile()
        {
            if (LockingStream is not null)
            { throw new InvalidOperationException("The file is already locked."); }

            LockingStream = new FileStream(OutputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public void Dispose()
            => LockingStream?.Dispose();
    }
}
