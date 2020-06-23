using ClangSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using static ClangSharpTest2020.CodeWriter;

namespace ClangSharpTest2020
{
    public sealed class TranslatedRecord : TranslatedDeclaration, IDeclarationContainer
    {
        internal RecordDecl Record { get; }

        private readonly List<TranslatedDeclaration> _Members = new List<TranslatedDeclaration>();
        public ReadOnlyCollection<TranslatedDeclaration> Members { get; }

        public override string DefaultName { get; }
        private readonly bool WasAnonymous = false;
        public long Size { get; }

        public override bool CanBeRoot => true;

        internal TranslatedVTableField VTableField { get; }
        internal TranslatedVTable VTable { get; }

        /// <summary>The insertion point for new members.</summary>
        /// <remarks>
        /// If -1, new members are added to the end of the members list.
        /// This field is used to ensure that the member order from the input file is maintained.
        /// This is necessary because we enumerate fields from the layout of the record before processing cursors under this record.
        /// </remarks>
        private int InsertNewMembersHere = -1;
        TranslatedFile IDeclarationContainer.File => File;
        void IDeclarationContainer.AddDeclaration(TranslatedDeclaration declaration)
        {
            Debug.Assert(ReferenceEquals(declaration.Parent, this), "Members should already have us as parents before being added.");
            Debug.Assert(_Members.IndexOf(declaration) == -1, "Members should not be added to records which they're already a member.");

            // If we have an insert point, use it to add the declaration
            if (InsertNewMembersHere != -1)
            {
                _Members.Insert(InsertNewMembersHere, declaration);
                InsertNewMembersHere++;
            }
            // Otherwise add the member to the end of the list
            else
            { _Members.Add(declaration); }
        }

        void IDeclarationContainer.RemoveDeclaration(TranslatedDeclaration declaration)
        {
            Debug.Assert(ReferenceEquals(declaration.Parent, this), "Only members which have us as parents should be removed.");

            // Find the index of the declaration to be removed
            int i = _Members.IndexOf(declaration);

            // If the delcaration isn't a member of this record, assert and do nothing
            if (i == -1)
            {
                Debug.Assert(false, "The index of a member belonging to us should be able to be found.");
                return;
            }

            // Remove the member
            _Members.RemoveAt(i);

            // If this member was before the insertion index, decrement the insertion index
            if (i < InsertNewMembersHere)
            { InsertNewMembersHere--; }
        }

        internal unsafe TranslatedRecord(IDeclarationContainer container, RecordDecl record)
            : base(container)
        {
            if (!record.Handle.IsDefinition)
            { throw new ArgumentException("Only defining records can be translated!"); }

            Record = record;
            Declaration = Record;
            Accessibility = Declaration.Access.ToTranslationAccessModifier();
            Members = _Members.AsReadOnly();

            DefaultName = Record.Name;

            if (String.IsNullOrEmpty(DefaultName))
            {
                WasAnonymous = true;

                // Note: Do not assert Record.IsAnonymousStructOrUnion here. It is false when an anonymous union has a named field.
                // Note: Do not emit a diagnostic here, anonymous records don't indicate an issue in C++.
                if (Record.IsUnion)
                { DefaultName = Parent.GetNameForUnnamed("Union"); }
                else if (Record is CXXRecordDecl cxxRecord && cxxRecord.IsClass)
                { DefaultName = Parent.GetNameForUnnamed("Class"); }
                else
                { DefaultName = Parent.GetNameForUnnamed("Struct"); }
            }

            // Process the layout
            long size;
            TranslatedVTableField vTableField;
            TranslatedVTable vTable;
            ProcessLayout(out size, out vTableField, out vTable);
            Size = size;
            VTableField = vTableField;
            VTable = vTable;

            // Determine the initial position for new members
            // This will be the index of either:
            // * The first normal field
            // * The first unimplemented field
            // * The first non-field
            // * The end of the list
            for (InsertNewMembersHere = 0; InsertNewMembersHere < Members.Count; InsertNewMembersHere++)
            {
                TranslatedDeclaration member = Members[InsertNewMembersHere];

                if (member is TranslatedNormalField)
                { break; }
                else if (member is TranslatedUnimplementedField)
                { break; }
                else if (!(member is TranslatedField))
                { break; }
            }

            // Process any other nested cursors which aren't fields or methods
            foreach (Cursor cursor in Record.CursorChildren)
            {
                // Fields were processed earlier when we processed the layout so they have special handling.
                if (cursor is FieldDecl field)
                {
                    HandleFieldFoundWhileProcessingChildren(field);
                    continue;
                }

                // All other children are handled by the normal cursor processing.
                File.ProcessCursor(this, cursor);
            }
        }

        private unsafe void ProcessLayout(out long recordSize, out TranslatedVTableField vTableField, out TranslatedVTable vTable)
        {
            PathogenRecordLayout* layout = null;
            try
            {
                layout = PathogenExtensions.pathogen_GetRecordLayout(Record.Handle);

                if (layout == null)
                {
                    File.Diagnostic(Severity.Fatal, Record, $"Failed to get the record layout of {Record.Name}");
                    recordSize = 0;
                    vTableField = null;
                    vTable = null;
                    return;
                }

                // Add fields from layout
                vTableField = null;
                TranslatedBaseField baseFieldAt0 = null;
                for (PathogenRecordField* field = layout->FirstField; field != null; field = field->NextField)
                {
                    TranslatedField newField = TranslatedField.Create(this, field);

                    if (newField is TranslatedVTableField newVTableField)
                    {
                        if (vTableField is null)
                        { vTableField = newVTableField; }
                        else
                        { File.Diagnostic(Severity.Warning, Record, $"Unimplemented translation: Record has more than one vTable pointer. (First at {vTableField.Offset}, another at {newField.Offset}.)"); }
                    }

                    if (newField is TranslatedBaseField newBaseField && newBaseField.Offset == 0)
                    {
                        Debug.Assert(baseFieldAt0 is null, "It shouldn't be possible for more than one base field to be at offset 0.");
                        baseFieldAt0 = newBaseField;
                    }
                }

                // Add vTable type
                if (layout->FirstVTable != null)
                {
                    // Synthesize a vTable field 
                    if (vTableField is null)
                    {
                        if (baseFieldAt0 is null)
                        { File.Diagnostic(Severity.Warning, Record, "Unimplemented translation: Record has vTable but no vTable pointer nor base at offset 0."); }
                        else
                        { vTableField = new TranslatedVTableField(this, baseFieldAt0); }
                    }

                    // If we don't have a vTable field and we couldn't synthesize a vTable field, we can't have a vTable
                    if (vTableField is null)
                    { vTable = null; }
                    else
                    { vTable = new TranslatedVTable(vTableField, layout->FirstVTable); }

                    if (layout->FirstVTable->NextVTable != null)
                    { File.Diagnostic(Severity.Warning, Record, $"Unimplemented translation: Record has more than on vTable."); }
                }
                else
                { vTable = null; }

                // Return the size
                recordSize = layout->Size;
            }
            finally
            {
                if (layout != null)
                { PathogenExtensions.pathogen_DeleteRecordLayout(layout); }
            }
        }

        /// <summary>Handle a field found while processing cursors which were children of this record.</summary>
        /// <param name="field">The field to handle.</param>
        /// <remarks>
        /// Fields are added as members to this record by looking at the field layout. As such, we've already added them by the time we handle field declarations.
        /// 
        /// This method does two things:
        /// 1) It ensures the processed field cursor has a corresponding member in this record.
        /// 2) It uses the processed field cursor to maintain member order from the input file.
        ///    (We can't just move the found fields to the end of the record because that would result in implementation details like virtual bases being at the start of the translation.)
        ///    In short, it ensures that new members are inserted before the field after the one specified by <paramref name="fieldDeclaration"/>.
        /// </remarks>
        private void HandleFieldFoundWhileProcessingChildren(FieldDecl fieldDeclaration)
        {
            bool foundCorrespondingField = false;

            int fieldIndex = 0;
            foreach (TranslatedDeclaration member in Members)
            {
                if (member is TranslatedField field)
                {
                    // Check if we've found the corresponding field
                    if (field is TranslatedNormalField normalField && normalField.Field == fieldDeclaration)
                    {
                        foundCorrespondingField = true;
                        break;
                    }
                }

                fieldIndex++;
            }

            // If we found the corresponding field, update our insertion point to be right after this field
            if (foundCorrespondingField)
            { InsertNewMembersHere = fieldIndex + 1; }
            // Otherwise we complain about the unexpected field (keeping the old insertion point.)
            else
            { File.Diagnostic(Severity.Warning, fieldDeclaration, "Field does not exist in the record's layout."); }
        }

        protected override void TranslateImplementation(CodeWriter writer)
        {
            writer.Using("System.Runtime.InteropServices");

            writer.EnsureSeparation();
            //TODO: Documentation comment
            writer.WriteLine($"[StructLayout(LayoutKind.Explicit, Size = {Size})]");
            // Records are translated as ref structs to prevent storing them on the managed heap.
            // If we decide to support normal structs later on, the following uses of Unsafe become invalid:
            // * TranslatedNormalField.TranslateConstantArrayField
            writer.WriteLine($"{Accessibility.ToCSharpKeyword()} unsafe ref partial struct {SanitizeIdentifier(TranslatedName)}");
            using (writer.Block())
            {
                // Write out members
                foreach (TranslatedDeclaration member in _Members)
                { member.Translate(writer); }
            }

            // Mark the record as consumed
            File.Consume(Record);
        }

        private UnnamedNamer UnnamedNamer = null;
        internal string GetNameForUnnamed(string category)
        {
            // If this record was unnamed its self, it uses its parent for the name
            // This makes it so that unnamed counting is relative to the nearest nammed record or the file if we're an unnamed type at root.
            // This also prevents nested anonyomous types from using the same name, which causes CS0542: member names cannot be the same as their enclosing type
            if (WasAnonymous)
            { return Parent.GetNameForUnnamed(category); }

            if (UnnamedNamer is null)
            { UnnamedNamer = new UnnamedNamer(); }

            return UnnamedNamer.GetName(category);
        }

        string IDeclarationContainer.GetNameForUnnamed(string category)
            => GetNameForUnnamed(category);
    }
}
