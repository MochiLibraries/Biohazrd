using ClangSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ClangType = ClangSharp.Type;
using Type = System.Type;

namespace ClangSharpTest2020
{
    internal static class ClangSharpInfoDumper
    {
        private readonly struct InfoRow
        {
            public readonly bool IsMajorHeader;
            public readonly bool IsMinorHeader;
            public readonly string Label;
            public readonly string Value;
            public readonly object RawValue;

            public InfoRow(string label, string value, object rawValue)
            {
                IsMajorHeader = false;
                IsMinorHeader = false;
                Label = label;
                RawValue = rawValue;

                if (value is null)
                { value = "<null>"; }
                else if (value.Length == 0)
                { value = "<empty>"; }
                // Bit of a hack, try to make paths prettier
                else if (Path.IsPathFullyQualified(value))
                { value = Path.GetFileName(value); }

                Value = value;
            }

            public InfoRow(string label, object rawValue)
                : this(label, rawValue?.ToString(), rawValue)
            { }

            private InfoRow(string header, bool isMajorHeader = false, bool isMinorHeader = false)
            {
                IsMajorHeader = isMajorHeader;
                IsMinorHeader = isMinorHeader;
                Label = header;
                Value = null;
                RawValue = null;
            }

            public static InfoRow MajorHeader(string header)
                => new InfoRow(header, true, false);

            public static InfoRow MinorHeader(string header)
                => new InfoRow(header, false, true);
        }

        private abstract class Dumper
        {
            public abstract IEnumerable<InfoRow> Dump(object target);
        }

        private abstract class Dumper<T> : Dumper
        {
            protected abstract IEnumerable<InfoRow> DumpT(T target);

            public override IEnumerable<InfoRow> Dump(object target)
                => DumpT((T)target);
        }

        private class CursorDumper : Dumper<Cursor>
        {
            protected override IEnumerable<InfoRow> DumpT(Cursor target)
            {
                string locationLabel = "Location";

                if (GlobalConfiguration.IncludeAllLocationDataInDump)
                { locationLabel = "Spelling Location"; }

                yield return new InfoRow(locationLabel, ClangSharpLocationHelper.GetFriendlyLocation(target, includeFileKindInfo: true), null);

                if (GlobalConfiguration.IncludeAllLocationDataInDump)
                {
                    yield return new InfoRow("Expansion Location", ClangSharpLocationHelper.GetFriendlyLocation(target, ClangSharpLocationHelper.Kind.ExpansionLocation), null);
                    yield return new InfoRow("File Location", ClangSharpLocationHelper.GetFriendlyLocation(target, ClangSharpLocationHelper.Kind.FileLocation), null);
                    yield return new InfoRow("Presumed Location", ClangSharpLocationHelper.GetFriendlyLocation(target, ClangSharpLocationHelper.Kind.PresumedLocation), null);
                }
            }
        }

        private class ReflectionDumper : Dumper
        {
            private readonly Type Type;
            private readonly List<PropertyOrFieldInfo> DataMembers;

            public ReflectionDumper(Type type)
            {
                Type = type;

                BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.DoNotWrapExceptions;
                FieldInfo[] fields = type.GetFields(bindingFlags);
                PropertyInfo[] properties = type.GetProperties(bindingFlags);

                DataMembers = new List<PropertyOrFieldInfo>(capacity: fields.Length + properties.Length);

                foreach (FieldInfo field in fields)
                { DataMembers.Add(new PropertyOrFieldInfo(field)); }
                
                foreach (PropertyInfo property in properties)
                {
                    if (!PropertyFilter(Type, property))
                    { continue; }

                    DataMembers.Add(new PropertyOrFieldInfo(property));
                }
            }

            private static bool PropertyFilter(Type type, PropertyInfo property)
            {
                // Set-only properties aren't useful
                if (!property.CanRead)
                { return false; }

                // Collections aren't useful for printing
                if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                { return false; }

                // If we got this far, the property is OK
                return true;
            }

            public override IEnumerable<InfoRow> Dump(object target)
            {
                yield return InfoRow.MinorHeader(Type.Name);

                foreach (PropertyOrFieldInfo dataMember in DataMembers)
                {
                    object rawValue;
                    string value;

                    try
                    {
                        rawValue = dataMember.GetValue(target);
                        value = rawValue?.ToString();
                    }
                    catch (Exception ex)
                    {
                        rawValue = ex;
                        value = $"{ex.GetType()}: {ex.Message}";
                    }

                    // If the value is a declaration, add location info
                    // (Except for translation units because their ToString prints the file name already.)
                    if (rawValue is Cursor cursor && !(cursor is TranslationUnitDecl))
                    { value += $" @ {ClangSharpLocationHelper.GetFriendlyLocation(cursor)}"; }

                    yield return new InfoRow(dataMember.Name, value, rawValue);
                }
            }
        }

        private class FunctionDeclDumper : ReflectionDumper
        {
            public FunctionDeclDumper()
                : base(typeof(FunctionDecl))
            { }

            public override IEnumerable<InfoRow> Dump(object target)
            {
                foreach (InfoRow row in base.Dump(target))
                { yield return row; }

                FunctionDecl targetFunction = (FunctionDecl)target;
                PathogenOperatorOverloadInfo info = targetFunction.GetOperatorOverloadInfo();
                bool isOperatorOverload = info.Kind != PathogenOperatorOverloadKind.None;
                yield return new InfoRow("IsOperatorOverload", isOperatorOverload.ToString(), isOperatorOverload);

                if (isOperatorOverload)
                {
                    yield return InfoRow.MinorHeader("Operator overload info");
                    yield return new InfoRow(nameof(info.Kind), info.Kind);
                    yield return new InfoRow(nameof(info.Name), info.Name);
                    yield return new InfoRow(nameof(info.Spelling), info.Spelling);
                    yield return new InfoRow(nameof(info.IsUnary), info.IsUnary);
                    yield return new InfoRow(nameof(info.IsBinary), info.IsBinary);
                    yield return new InfoRow(nameof(info.IsMemberOnly), info.IsMemberOnly);
                }
            }
        }

        private static readonly Dictionary<Type, Dumper> Dumpers = new Dictionary<Type, Dumper>()
        {
            { typeof(Cursor), new CursorDumper() },
            { typeof(FunctionDecl), new FunctionDeclDumper() },
        };

        private static void EnumerateRows(List<InfoRow> rows, Type type, Type endType, object target)
        {
            // Enumerate the rows from the parent type first (unless this is the cursor type)
            if (type != endType)
            { EnumerateRows(rows, type.BaseType, endType, target); }

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

        private static void AddTypeInfo(List<InfoRow> rows, string label, ClangType clangType)
        {
            rows.Add(InfoRow.MajorHeader($"{label} -- {clangType.Kind}/{clangType.GetType().Name} -- {clangType}"));
            EnumerateRows(rows, clangType.GetType(), typeof(ClangType), clangType);

            // For tag types, enumerate the declaration
            if (clangType is TagType tagType && tagType.Decl is object)
            {
                rows.Add(InfoRow.MajorHeader($"{label}.Decl -- {tagType.Decl.CursorKindDetailed()} -- {tagType.Decl}"));
                EnumerateRows(rows, tagType.Decl.GetType(), typeof(Cursor), tagType.Decl);
            }

            // Add type info related types (like the type pointed to by a pointer.)
            if (GlobalConfiguration.DumpClangTypeDetailsRecursively)
            {
                // Function types are a special case
                if (clangType is FunctionType functionType)
                {
                    AddTypeInfo(rows, $"{label}.{nameof(functionType.ReturnType)}", functionType.ReturnType);

                    if (functionType is FunctionProtoType functionProtoType)
                    {
                        int i = 0;
                        foreach (ClangType parameterType in functionProtoType.ParamTypes)
                        {
                            AddTypeInfo(rows, $"{label}.{nameof(functionProtoType.ParamTypes)}[{i}]", parameterType);
                            i++;
                        }
                    }
                }

                (ClangType nestedType, string subLabel) = clangType switch
                {
                    PointerType pointerType => (pointerType.PointeeType, nameof(pointerType.PointeeType)),
                    ReferenceType referenceType => (referenceType.PointeeType, nameof(referenceType.PointeeType)),
                    ArrayType arrayType => (arrayType.ElementType, nameof(arrayType.ElementType)),
                    AttributedType attributedType => (attributedType.ModifiedType, nameof(attributedType.ModifiedType)),
                    ElaboratedType elaboratedType => (elaboratedType.NamedType, nameof(elaboratedType.NamedType)),
                    TypedefType typedefType => (typedefType.CanonicalType, nameof(typedefType.CanonicalType)),
                    _ => default
                };

                if (nestedType is object)
                {
                    AddTypeInfo(rows, $"{label}.{subLabel}", nestedType);
                }
            }
        }

        public static void Dump(TextWriter writer, Cursor cursor)
        {
            List<InfoRow> rows = new List<InfoRow>();

            // Add the cursor header row
            rows.Add(InfoRow.MajorHeader($"{cursor.CursorKindDetailed()} {cursor}"));

            // Add cursor rows
            EnumerateRows(rows, cursor.GetType(), typeof(Cursor), cursor);

            // Find all type fields so we can dump them too
            if (GlobalConfiguration.IncludeClangTypeDetailsInDump)
            {
                int endOfCursorInfo = rows.Count;
                for (int i = 0; i < endOfCursorInfo; i++)
                {
                    InfoRow row = rows[i];

                    if (row.RawValue is ClangType clangType)
                    { AddTypeInfo(rows, row.Label, clangType); }
                }
            }

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
            string headerBorder = new String('=', headerBorderLength);

            foreach (InfoRow row in rows)
            {
                // Handle major heading rows
                if (row.IsMajorHeader)
                {
                    writer.WriteLine(headerBorder);
                    writer.WriteLine(row.Label);
                    writer.WriteLine(headerBorder);
                    continue;
                }

                // Handle minor heading rows
                if (row.IsMinorHeader)
                {
                    const string headingRowPrefix = "---- ";
                    writer.Write(headingRowPrefix);
                    writer.Write(row.Label);

                    for (int i = headingRowPrefix.Length + row.Label.Length; i < headerBorderLength; i++)
                    { writer.Write('-'); }

                    writer.WriteLine();
                    continue;
                }

                // Write normal rows
                writer.Write(row.Label);

                for (int i = row.Label.Length; i < longestLabel; i++)
                { writer.Write(' '); }

                writer.Write(columnSeparator);
                writer.WriteLine(row.Value);
            }
        }
    }
}
