using ClangSharp.Interop;
using System;

namespace Biohazrd
{
    public struct SourceLocation
    {
        public readonly string SourceFile;
        public readonly int Line;
        public readonly int Column;

        public bool IsNull => SourceFile == null && Line == 0 && Column == 0;

        public SourceLocation(string sourceFile, int line, int column)
        {
            SourceFile = sourceFile;
            Line = line;
            Column = column;
        }

        public SourceLocation(string sourceFile, int line)
            : this(sourceFile, line, 0)
        { }

        public SourceLocation(string sourceFile)
            : this(sourceFile, 0, 0)
        { }

        public SourceLocation(CXSourceLocation clangLocation)
        {
            clangLocation.GetFileLocation(out CXFile file, out uint clangLine, out uint clangColumn, out _);
            SourceFile = file.Name.ToString();
            Line = checked((int)clangLine);
            Column = checked((int)clangColumn);
        }

        public string ToString(bool includeColumn)
        {
            // For a null source location, just return an empty string
            if (IsNull)
            { return String.Empty; }

            string sourceFile = SourceFile ?? "<null>";

            if (Line == 0)
            { return sourceFile; }

            if (Column != 0 && includeColumn)
            { return $"{sourceFile}:{Line}:{Column}"; }

            return $"{sourceFile}:{Line}";
        }

        public override string ToString()
            => ToString(false);

        public static SourceLocation Null => default;
    }
}
