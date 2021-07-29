using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Biohazrd.OutputGeneration
{
    [ProvidesOutputSessionFactory]
    public class CppCodeWriter : CLikeCodeWriter
    {
        private SortedSet<string> IncludeFiles = new SortedSet<string>(StringComparer.InvariantCulture);
        private SortedSet<string> SystemIncludeFiles = new SortedSet<string>(StringComparer.InvariantCulture);

        protected string FileDirectoryPath { get; }

        protected CppCodeWriter(OutputSession session, string filePath)
            : base(session, filePath)
            => FileDirectoryPath = Path.GetDirectoryName(FilePath) ?? ".";

        private static OutputSession.WriterFactory<CppCodeWriter> FactoryMethod => (session, filePath) => new CppCodeWriter(session, filePath);

        public void Include(string filePath, bool systemInclude = false)
        {
            // To keep paths normalized and simpler to consume by custom comprers, we ensure all paths are absolute
            // They are later written out as relative
            if (!Path.IsPathFullyQualified(filePath))
            {
                if (Path.IsPathRooted(filePath))
                { filePath = Path.GetFullPath(filePath); }
                else
                { filePath = Path.GetFullPath(Path.Combine(FileDirectoryPath, filePath)); }
            }

            (systemInclude ? SystemIncludeFiles : IncludeFiles).Add(filePath);
        }

        /// <summary>Sets a custom comparer for customizing the sort order of include files</summary>
        /// <remarks>The file paths provided to the comparer will always be absolute paths, even if they were specified as relative</remarks>
        public void SetIncludeComparer(IComparer<string> comparer)
        {
            IncludeFiles = new SortedSet<string>(IncludeFiles, comparer);
            SystemIncludeFiles = new SortedSet<string>(SystemIncludeFiles, comparer);
        }

        /// <summary>Sets a custom comparer for customizing the sort order of include files</summary>
        /// <remarks>The file paths provided to the comparer will always be absolute paths, even if they were specified as relative</remarks>
        public void SetIncludeComparer(Comparison<string> comparison)
            => SetIncludeComparer(Comparer<string>.Create(comparison));

        public LeftAdjustedScope DisableScope(bool disabled, string? message = null)
        {
            if (!disabled)
            { return default; }

            EnsureSeparation();

            LeftAdjustedScope ret;

            if (message is null)
            { ret = CreateLeftAdjustedScope("#if 0", "#endif"); }
            else
            { ret = CreateLeftAdjustedScope($"#if 0 // {message}", "#endif"); }

            NoSeparationNeededBeforeNextLine();
            return ret;
        }

        public LeftAdjustedScope DisableScope(string? message = null)
            => DisableScope(true, message);

        private string NormalizeIncludeFilePath(string filePath)
        {
            // Get the path relative to our output file
            filePath = Path.GetRelativePath(FileDirectoryPath, filePath);

            // Use forward slashes so the generated file is portable
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            { filePath = filePath.Replace('\\', '/'); }

            return filePath;
        }

        protected override void WriteBetweenHeaderAndCode(StreamWriter writer)
        {
            foreach (string includeFile in IncludeFiles)
            { writer.WriteLine($"#include \"{NormalizeIncludeFilePath(includeFile)}\""); }

            if (IncludeFiles.Count > 0)
            { writer.WriteLine(); }

            foreach (string includeFile in SystemIncludeFiles)
            { writer.WriteLine($"#include <{NormalizeIncludeFilePath(includeFile)}>"); }

            if (SystemIncludeFiles.Count > 0)
            { writer.WriteLine(); }
        }
    }
}
