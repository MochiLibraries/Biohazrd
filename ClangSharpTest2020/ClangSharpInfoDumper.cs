//#define VERBOSE_LOCATION_INFO
using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Type = System.Type;

namespace ClangSharpTest2020
{
    internal static class ClangSharpInfoDumper
    {
        private readonly struct InfoRow
        {
            public readonly string Label;
            public readonly string Value;

            public InfoRow(string label, string value)
            {
                Label = label;

                if (value is null)
                { value = "<null>"; }
                else if (value.Length == 0)
                { value = "<empty>"; }
                // Bit of a hack, try to make paths prettier
                else if (Path.IsPathFullyQualified(value))
                { value = Path.GetFileName(value); }

                Value = value;
            }

            public InfoRow(string header)
            {
                Label = header;
                Value = null;
            }
        }

        private abstract class Dumper
        {
            public abstract IEnumerable<InfoRow> Dump(Cursor target);
        }

        private abstract class Dumper<T> : Dumper
            where T : Cursor
        {
            protected abstract IEnumerable<InfoRow> DumpT(T target);

            public override IEnumerable<InfoRow> Dump(Cursor target)
                => DumpT((T)target);
        }

        private class CursorDumper : Dumper<Cursor>
        {
            protected override IEnumerable<InfoRow> DumpT(Cursor target)
            {
                CXFile startFile;
                uint startLine;
                uint startColumn;
                uint startOffset;
                CXFile endFile;
                uint endLine;
                uint endColumn;
                uint endOffset;

                // Spelling location
                target.Extent.Start.GetSpellingLocation(out startFile, out startLine, out startColumn, out startOffset);
                target.Extent.End.GetSpellingLocation(out endFile, out endLine, out endColumn, out endOffset);
                string location = FormatLocation(startFile, startLine, startColumn, startOffset, endFile, endLine, endColumn, endOffset);

                if (target.Extent.Start.IsFromMainFilePathogen() || target.Extent.End.IsFromMainFilePathogen())
                { location += " <MainFilePgn>"; }

                if (target.Extent.Start.IsInSystemHeader || target.Extent.End.IsInSystemHeader)
                { location += " <SystemHeader>"; }

                string locationLabel = "Location";
#if VERBOSE_LOCATION_INFO
                locationLabel = "Spelling Location";
#endif

                yield return new InfoRow(locationLabel, location);

#if VERBOSE_LOCATION_INFO
                // Note: Instantiation location is not printed here because it is deprecated, it was replaced by the expansion location.
                // Expansion location
                target.Extent.Start.GetExpansionLocation(out startFile, out startLine, out startColumn, out startOffset);
                target.Extent.End.GetExpansionLocation(out endFile, out endLine, out endColumn, out endOffset);
                location = FormatLocation(startFile, startLine, startColumn, startOffset, endFile, endLine, endColumn, endOffset);
                yield return new InfoRow("Expansion Location", location);

                // File location
                target.Extent.Start.GetFileLocation(out startFile, out startLine, out startColumn, out startOffset);
                target.Extent.End.GetFileLocation(out endFile, out endLine, out endColumn, out endOffset);
                location = FormatLocation(startFile, startLine, startColumn, startOffset, endFile, endLine, endColumn, endOffset);
                yield return new InfoRow("File Location", location);

                // Presumed location
                CXString startFileName;
                CXString endFileName;
                target.Extent.Start.GetPresumedLocation(out startFileName, out startLine, out startColumn);
                target.Extent.End.GetPresumedLocation(out endFileName, out endLine, out endColumn);
                location = FormatLocation(startFile.ToString(), startLine, startColumn, 0, endFile.ToString(), endLine, endColumn, 0);
                yield return new InfoRow("Presumed Location", location);
#endif
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

        private class ReflectionDumper : Dumper
        {
            private readonly Type Type;
            private readonly FieldInfo[] Fields;
            private readonly List<PropertyInfo> Properties;

            public ReflectionDumper(Type type)
            {
                Type = type;

                BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.DoNotWrapExceptions;
                Fields = type.GetFields(bindingFlags);

                Properties = new List<PropertyInfo>();
                foreach (PropertyInfo property in type.GetProperties(bindingFlags))
                {
                    if (!PropertyFilter(Type, property))
                    { continue; }

                    Properties.Add(property);
                }
            }

            private static bool PropertyFilter(Type type, PropertyInfo property)
            {
                // Collections aren't useful for printing
                if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                { return false; }

                // If we got this far, the property is OK
                return true;
            }

            public override IEnumerable<InfoRow> Dump(Cursor target)
            {
                yield return new InfoRow(Type.Name);

                foreach (FieldInfo field in Fields)
                {
                    string value;

                    try
                    { value = field.GetValue(target)?.ToString(); }
                    catch (Exception ex)
                    { value = $"{ex.GetType()}: {ex.Message}"; }

                    yield return new InfoRow(field.Name, value);
                }

                foreach (PropertyInfo property in Properties)
                {
                    if (!property.CanRead)
                    { continue; }

                    string value;

                    try
                    { value = property.GetValue(target)?.ToString(); }
                    catch (Exception ex)
                    { value = $"{ex.GetType()}: {ex.Message}"; }

                    yield return new InfoRow(property.Name, value);
                }
            }
        }

        private static readonly Dictionary<Type, Dumper> Dumpers = new Dictionary<Type, Dumper>()
        {
            { typeof(Cursor), new CursorDumper() }
        };

        private static void EnumerateRows(List<InfoRow> rows, Type type, Cursor target)
        {
            // Enumerate the rows from the parent type first (unless this is the cursor type)
            if (type != typeof(Cursor))
            { EnumerateRows(rows, type.BaseType, target); }

            // Check if we have a dumper for this type
            Dumper dumper;
            if (!Dumpers.TryGetValue(type, out dumper))
            {
                dumper = new ReflectionDumper(type);
                Dumpers.Add(type, dumper);
            }

            // Enumerate the rows
            rows.AddRange(dumper.Dump(target));
        }

        public static void Dump(TextWriter writer, Cursor cursor)
        {
            List<InfoRow> rows = new List<InfoRow>();
            EnumerateRows(rows, cursor.GetType(), cursor);

            // Determine the longest key and value for formatting purposes
            int longestLabel = 0;
            int longestValue = 0;
            foreach (InfoRow row in rows)
            {
                if (row.Label.Length > longestLabel)
                { longestLabel = row.Label.Length; }

                if (row.Value is object && row.Value.Length > longestValue)
                { longestValue = row.Value.Length; }
            }

            // Write out the information dump
            const string columnSeparator = " | ";
            int headerBorderLength = longestLabel + longestValue + columnSeparator.Length;
            string header = $"{cursor.CursorKindDetailed()} {cursor}";

            if (header.Length > headerBorderLength)
            { headerBorderLength = header.Length; }

            string headerBorder = new String('=', headerBorderLength);
            writer.WriteLine(headerBorder);
            writer.WriteLine(header);
            writer.WriteLine(headerBorder);

            foreach (InfoRow row in rows)
            {
                // Handle heading rows
                if (row.Value is null)
                {
                    const string headingRowPrefix = "---- ";
                    writer.Write(headingRowPrefix);
                    writer.Write(row.Label);

                    for (int i = headingRowPrefix.Length + row.Label.Length; i < headerBorderLength; i++)
                    { writer.Write('-'); }

                    writer.WriteLine();
                    continue;
                }

                writer.Write(row.Label);

                for (int i = row.Label.Length; i < longestLabel; i++)
                { writer.Write(' '); }

                writer.Write(columnSeparator);
                writer.WriteLine(row.Value);
            }
        }
    }
}
