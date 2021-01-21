using ClangSharp;
using ClangSharp.Pathogen;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Biohazrd
{
    public sealed record TranslatedRecord : TranslatedDeclaration
    {
        public ImmutableList<TranslatedDeclaration> Members { get; init; } = ImmutableList<TranslatedDeclaration>.Empty;

        public TranslatedBaseField? NonVirtualBaseField { get; init; }
        public TranslatedVTableField? VTableField { get; init; }
        public TranslatedVTable? VTable { get; init; }

        public ImmutableList<TranslatedDeclaration> UnsupportedMembers { get; init; } = ImmutableList<TranslatedDeclaration>.Empty;

        public long Size { get; init; }

        public RecordKind Kind { get; init; }
        public bool MustBePassedByReference { get; init; }

        internal unsafe TranslatedRecord(TranslationUnitParser parsingContext, TranslatedFile file, RecordDecl record)
            : base(file, record)
        {
            if (!record.Handle.IsDefinition)
            { throw new ArgumentException("Only defining records can be translated!"); }

            MustBePassedByReference = record.MustBePassedByReference(isForInstanceMethodReturnValue: false);

            if (record.IsStruct)
            { Kind = RecordKind.Struct; }
            else if (record.IsUnion)
            { Kind = RecordKind.Union; }
            else if (record is CXXRecordDecl cxxRecord && cxxRecord.IsClass)
            { Kind = RecordKind.Class; }
            else
            { Kind = RecordKind.Unknown; }

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

            foreach (Cursor cursor in record.CursorChildren)
            {
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

                    // If the cursor is an anonymous union, emit the field corresponding to that union
                    if (childDeclaration is TranslatedRecord { Kind: RecordKind.Union, IsUnnamed: true, Declaration: RecordDecl anonymousUnion })
                    {
                        foreach ((FieldDecl fieldDeclaration, TranslatedNormalField normalField) in normalFields)
                        {
                            // If the type of this field matches the type of this union, emit the field now.
                            if (fieldDeclaration.Type == anonymousUnion.TypeForDecl)
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

        /// <summary>The total count of all members in this record, not just the ones contained within <see cref="Members"/>.</summary>
        public int TotalMemberCount
        {
            get
            {
                int ret = Members.Count + UnsupportedMembers.Count;

                if (NonVirtualBaseField is not null)
                { ret++; }

                if (VTableField is not null)
                { ret++; }

                if (VTable is not null)
                { ret++; }

                return ret;
            }
        }

        public override IEnumerator<TranslatedDeclaration> GetEnumerator()
        {
            foreach (TranslatedDeclaration member in Members)
            { yield return member; }

            if (NonVirtualBaseField is not null)
            { yield return NonVirtualBaseField; }

            if (VTableField is not null)
            { yield return VTableField; }

            if (VTable is not null)
            { yield return VTable; }

            foreach (TranslatedDeclaration unsupportedMember in UnsupportedMembers)
            { yield return unsupportedMember; }
        }

        public override string ToString()
            => $"Record {base.ToString()}";
    }
}
