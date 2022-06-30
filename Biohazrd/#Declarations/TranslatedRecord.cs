﻿using Biohazrd.Infrastructure;
using Biohazrd.Metadata;
using ClangSharp;
using ClangSharp.Pathogen;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Biohazrd
{
    public partial record TranslatedRecord : TranslatedDeclaration
    {
        [CatchAllMembersProperty]
        public ImmutableList<TranslatedDeclaration> Members { get; init; } = ImmutableList<TranslatedDeclaration>.Empty;

        public TranslatedBaseField? NonVirtualBaseField { get; init; }
        public TranslatedVTableField? VTableField { get; init; }
        public TranslatedVTable? VTable { get; init; }

        public ImmutableList<TranslatedDeclaration> UnsupportedMembers { get; init; } = ImmutableList<TranslatedDeclaration>.Empty;

        public long Size { get; init; }

        public RecordKind Kind { get; init; }

        internal unsafe TranslatedRecord(TranslationUnitParser parsingContext, TranslatedFile file, RecordDecl record)
            : base(file, record)
        {
            if (!record.Handle.IsDefinition)
            { throw new ArgumentException("Only defining records can be translated!"); }

            if (record.IsStruct)
            { Kind = RecordKind.Struct; }
            else if (record.IsUnion)
            { Kind = RecordKind.Union; }
            else if (record is CXXRecordDecl cxxRecord && cxxRecord.IsClass)
            { Kind = RecordKind.Class; }
            else
            { Kind = RecordKind.Unknown; }

            // If this record is anonymous, implicitly mark it as lazily generated
            if (IsUnnamed)
            { Metadata = Metadata.Add<LazilyGenerated>(); }

            // Process layout and vtables
            // Normal fields are stored in this dictionary and added as they are encountered in the cursor tree
            // This primarily allows us to easily ensure our member order matches the input file's declaration order
            Dictionary<FieldDecl, TranslatedNormalField> normalFields = new();
            PathogenRecordLayout* layout = null;
            try
            {
                layout = PathogenExtensions.pathogen_GetRecordLayout(record.Handle);

                if (layout == null)
                { Diagnostics = Diagnostics.Add(Severity.Fatal, record, $"Failed to get the record layout of {this}."); }
                else
                {
                    Size = layout->Size;

                    // Enumerate fields
                    for (PathogenRecordField* field = layout->FirstField; field != null; field = field->NextField)
                    {
                        TranslatedField newField = TranslatedField.Create(parsingContext, file, field);

                        switch (newField)
                        {
                            case TranslatedNormalField normalField when normalField.Declaration is FieldDecl fieldDeclaration:
                                normalFields.Add(fieldDeclaration, normalField);
                                break;
                            case TranslatedVTableField vTableField when VTableField is null:
                                VTableField = vTableField;
                                break;
                            case TranslatedBaseField baseField when baseField.Offset == 0 && NonVirtualBaseField is null:
                                NonVirtualBaseField = baseField;
                                break;
                            default:
                                UnsupportedMembers = UnsupportedMembers.Add(newField);
                                break;
                        }
                    }

                    // Create VTable types
                    for (PathogenVTable* vTable = layout->FirstVTable; vTable != null; vTable = vTable->NextVTable)
                    {
                        TranslatedVTable newVTable = new(parsingContext, file, vTable);

                        if (VTable is null)
                        { VTable = newVTable; }
                        else // Secondary VTables are not yet supported
                        { UnsupportedMembers = UnsupportedMembers.Add(VTable); }
                    }
                }
            }
            finally
            {
                if (layout != null)
                { PathogenExtensions.pathogen_DeleteRecordLayout(layout); }
            }

            // Process Clang cursor children
            ImmutableList<TranslatedDeclaration>.Builder membersBuilder = ImmutableList.CreateBuilder<TranslatedDeclaration>();
            IEnumerator<Cursor> children;
            bool usingRawDeclEnumerator;
            Decl canonicalDeclaration = record.CanonicalDecl;

            //TODO: Ideally we'd probably use RawDeclEnumerator for everything instead of having the split code path.
            // However, this would be a pretty fundamental change in how Biohazrd processes records, so for now we restrict it to template specializations since
            // it's primarily necessary for implicit template specializations, which don't have a libclang cursor representation since they don't have a lexical location.
            if (record.CursorChildren.Count == 0 && record is ClassTemplateSpecializationDecl)
            {
                children = new RawDeclEnumerator(record);
                usingRawDeclEnumerator = true;
            }
            else
            {
                children = record.CursorChildren.GetEnumerator();
                usingRawDeclEnumerator = false;
            }

            foreach (Cursor cursor in children)
            {
                // For some reason Clang's raw declaration list has the "same" record declaration present in the list
                // It's not the same pointer, I'm sure it represents something but I'm unsure what. We just skip it.
                if (usingRawDeclEnumerator && ((Decl)cursor).CanonicalDecl == canonicalDeclaration)
                { continue; }

                // We created fields earlier, so now we add them to the member list
                if (cursor is FieldDecl field)
                {
                    if (normalFields.Remove(field, out TranslatedNormalField? translatedField))
                    {
                        membersBuilder.Add(translatedField);
                        continue;
                    }
                    else
                    {
                        // We don't ever expect this to happen. If there's a field cursor in our children, it should've come up in the layout.
                        TranslatedUnsupportedDeclaration badField = new TranslatedUnsupportedDeclaration
                        (
                            file,
                            field,
                            Severity.Error,
                            $"Field '{field}' appeared in {this}'s cursor children, but not in its layout."
                        );
                        membersBuilder.Add(badField);
                        continue;
                    }
                }

                // All other children are handled by the normal cursor processing.
                foreach (TranslatedDeclaration childDeclaration in parsingContext.CreateDeclarations(cursor, File))
                {
                    membersBuilder.Add(childDeclaration);

                    // If the cursor is an anonymous union that doesn't have an explicit field, emit the implicit backing field corresponding to that union
                    if (childDeclaration is TranslatedRecord { IsUnnamed: true, Declaration: RecordDecl anonymousUnion })
                    {
                        foreach ((FieldDecl fieldDeclaration, TranslatedNormalField normalField) in normalFields)
                        {
                            // If the field is unnamed and the type of this field matches the type of this union, emit the field now.
                            if (normalField.IsUnnamed && fieldDeclaration.Type.CanonicalType == anonymousUnion.TypeForDecl.CanonicalType)
                            {
                                membersBuilder.Add(normalField);
                                bool success = normalFields.Remove(fieldDeclaration);
                                Debug.Assert(success, "The call to Remove must succeed.");
                                break;
                            }
                        }
                    }
                }
            }

            // Add any fields that weren't encountered while processing children to the unsupported members list
            // (This should never happen, so we probably don't want them to appear in the output.)
            foreach ((FieldDecl fieldDeclaration, TranslatedNormalField normalField) in normalFields)
            {
                TranslatedNormalField unsupportedField = normalField with
                {
                    Diagnostics = normalField.Diagnostics.Add(Severity.Warning, fieldDeclaration, $"Field was found while enumerating the layout of {this}, but was not encountered in Clang's cursor tree.")
                };
                UnsupportedMembers = UnsupportedMembers.Add(unsupportedField);
            }

            // Apply members
            Members = membersBuilder.ToImmutable();
        }

        public override string ToString()
            => $"Record {base.ToString()}";
    }
}
