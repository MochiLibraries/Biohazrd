using Biohazrd.OutputGeneration;
using System;
using System.Collections.Generic;
using System.IO;

namespace Biohazrd.CSharp
{
    [ProvidesOutputSessionFactory]
    public class CppCodeWriter : CLikeCodeWriter
    {
        private readonly SortedSet<string> IncludeFiles = new SortedSet<string>(StringComparer.InvariantCulture);

        protected string FileDirectoryPath { get; }

        protected CppCodeWriter(OutputSession session, string filePath)
            : base(session, filePath)
            => FileDirectoryPath = Path.GetDirectoryName(FilePath) ?? ".";

        private static OutputSession.WriterFactory<CppCodeWriter> FactoryMethod => (session, filePath) => new CppCodeWriter(session, filePath);

        public void Include(string filePath)
        {
            if (Path.IsPathRooted(filePath))
            { filePath = Path.GetRelativePath(FileDirectoryPath, filePath); }

            IncludeFiles.Add(filePath);
        }

        public LeftAdjustedScope DisableScope(bool disabled, string message)
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

        protected override void WriteBetweenHeaderAndCode(StreamWriter writer)
        {
            foreach (string includeFile in IncludeFiles)
            { writer.WriteLine($"#include \"{includeFile}\""); }

            if (IncludeFiles.Count > 0)
            { writer.WriteLine(); }
        }
    }
}
