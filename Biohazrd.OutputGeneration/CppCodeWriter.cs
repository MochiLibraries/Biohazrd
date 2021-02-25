using System;
using System.Collections.Generic;
using System.IO;

namespace Biohazrd.OutputGeneration
{
    [ProvidesOutputSessionFactory]
    public class CppCodeWriter : CLikeCodeWriter
    {
        private readonly SortedSet<string> IncludeFiles = new SortedSet<string>(StringComparer.InvariantCulture);
        private readonly SortedSet<string> SystemIncludeFiles = new SortedSet<string>(StringComparer.InvariantCulture);

        protected string FileDirectoryPath { get; }

        protected CppCodeWriter(OutputSession session, string filePath)
            : base(session, filePath)
            => FileDirectoryPath = Path.GetDirectoryName(FilePath) ?? ".";

        private static OutputSession.WriterFactory<CppCodeWriter> FactoryMethod => (session, filePath) => new CppCodeWriter(session, filePath);

        public void Include(string filePath, bool systemInclude = false)
        {
            if (Path.IsPathRooted(filePath))
            { filePath = Path.GetRelativePath(FileDirectoryPath, filePath); }

            (systemInclude ? SystemIncludeFiles : IncludeFiles).Add(filePath);
        }

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

        protected override void WriteBetweenHeaderAndCode(StreamWriter writer)
        {
            foreach (string includeFile in IncludeFiles)
            { writer.WriteLine($"#include \"{includeFile}\""); }

            if (IncludeFiles.Count > 0)
            { writer.WriteLine(); }

            foreach (string includeFile in SystemIncludeFiles)
            { writer.WriteLine($"#include <{includeFile}>"); }

            if (SystemIncludeFiles.Count > 0)
            { writer.WriteLine(); }
        }
    }
}
