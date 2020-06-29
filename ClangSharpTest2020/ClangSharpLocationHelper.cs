using ClangSharp;
using ClangSharp.Interop;
using System;
using System.IO;

namespace ClangSharpTest2020
{
    internal static class ClangSharpLocationHelper
    {
        /// <remarks>Instantiation location is not included here because it is deprecated, it was replaced by <see cref="ExpansionLocation"/>.</remarks>
        public enum Kind
        {
            Spelling,
            ExpansionLocation,
            FileLocation,
            PresumedLocation
        }

        public static string GetFriendlyLocation(Cursor cursor, Kind kind = Kind.Spelling, bool includeFileKindInfo = false)
        {
            CXFile startFile;
            uint startLine;
            uint startColumn;
            uint startOffset;
            CXFile endFile;
            uint endLine;
            uint endColumn;
            uint endOffset;
            CXString startFileName = default;
            CXString endFileName = default;

            switch (kind)
            {
                case Kind.Spelling:
                    cursor.Extent.Start.GetSpellingLocation(out startFile, out startLine, out startColumn, out startOffset);
                    cursor.Extent.End.GetSpellingLocation(out endFile, out endLine, out endColumn, out endOffset);
                    break;
                case Kind.ExpansionLocation:
                    cursor.Extent.Start.GetExpansionLocation(out startFile, out startLine, out startColumn, out startOffset);
                    cursor.Extent.End.GetExpansionLocation(out endFile, out endLine, out endColumn, out endOffset);
                    break;
                case Kind.FileLocation:
                    cursor.Extent.Start.GetFileLocation(out startFile, out startLine, out startColumn, out startOffset);
                    cursor.Extent.End.GetFileLocation(out endFile, out endLine, out endColumn, out endOffset);
                    break;
                case Kind.PresumedLocation:
                    cursor.Extent.Start.GetPresumedLocation(out startFileName, out startLine, out startColumn);
                    cursor.Extent.End.GetPresumedLocation(out endFileName, out endLine, out endColumn);
                    startFile = endFile = default;
                    startOffset = endOffset = default;
                    break;
                default:
                    throw new ArgumentException("The specified location kind is invalid.", nameof(kind));
            }

            string ret;

            if (kind != Kind.PresumedLocation)
            { ret = FormatLocation(startFile, startLine, startColumn, startOffset, endFile, endLine, endColumn, endOffset); }
            else
            { ret = FormatLocation(startFileName.ToString(), startLine, startColumn, 0, endFileName.ToString(), endLine, endColumn, 0); }

            if (includeFileKindInfo)
            {
                if (cursor.Extent.Start.IsFromMainFilePathogen() || cursor.Extent.End.IsFromMainFilePathogen())
                { ret += " <MainFilePgn>"; }

                if (cursor.Extent.Start.IsInSystemHeader || cursor.Extent.End.IsInSystemHeader)
                { ret += " <SystemHeader>"; }
            }

            return ret;
        }

        private static string FormatLocation(CXFile startFile, uint startLine, uint startColumn, uint startOffset, CXFile endFile, uint endLine, uint endColumn, uint endOffset)
                => FormatLocation(startFile.Name.ToString(), startLine, startColumn, startOffset, endFile.Name.ToString(), endLine, endColumn, endOffset);

        private static string FormatLocation(string startFileName, uint startLine, uint startColumn, uint startOffset, string endFileName, uint endLine, uint endColumn, uint endOffset)
        {
            startFileName = Path.GetFileName(startFileName);
            endFileName = Path.GetFileName(endFileName);

            string location = "";

            if (startFileName == endFileName)
            {
                location += startFileName;
                location += startLine == endLine ? $":{startLine}" : $":{startLine}..{endLine}";
                location += startColumn == endColumn ? $":{startColumn}" : $":{startColumn}..{endColumn}";

                if (startOffset != 0 && endOffset != 0)
                { location += startOffset == endOffset ? $"[{startOffset}]" : $"[{startOffset}..{endOffset}]"; }
            }
            else
            { location += $" {startFileName}:{startLine}:{startColumn}[{startOffset}]..{endFileName}:{endLine}:{endColumn}[{endOffset}]"; }

            return location;
        }
    }
}
