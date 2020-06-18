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

        public override string TranslatedName => Record.Name;
        public long Size { get; }

        public override bool CanBeRoot => true;

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
            Members = _Members.AsReadOnly();

            // Process the layout
            Size = ProcessLayout();

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

        private unsafe long ProcessLayout()
        {
            PathogenRecordLayout* layout = null;
            try
            {
                layout = PathogenExtensions.pathogen_GetRecordLayout(Record.Handle);

                if (layout == null)
                {
                    File.Diagnostic(Severity.Fatal, Record, $"Failed to get the record layout of {Record.Name}");
                    return 0;
                }

                // Add fields from layout
                for (PathogenRecordField* field = layout->FirstField; field != null; field = field->NextField)
                { TranslatedField.Create(this, field); }

                //TODO: VTable stuff

                return layout->Size;
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

            int newInsertNewMembersHere = 0;
            foreach (TranslatedDeclaration member in Members)
            {
                if (member is TranslatedField field)
                {
                    // If we already have our corresponding field, we've found the InsertNewMembersHere index (which is the field following the one we are processing) so stop searching
                    if (foundCorrespondingField)
                    { break; }

                    // Check if we've found the corresponding field
                    // (Note we keep iterating through members to update InsertNewMembersHere until we find the subsequent field.)
                    if (field is TranslatedNormalField normalField && normalField.Field == fieldDeclaration)
                    { foundCorrespondingField = true; }
                }

                // Keep track of the new index for insertion
                newInsertNewMembersHere++;
            }

            // If we found the corresponding field, update our insertion point
            if (foundCorrespondingField)
            { InsertNewMembersHere = newInsertNewMembersHere; }
            // Otherwise we complain about the unexpected field (keeping the old insertion point.)
            else
            { File.Diagnostic(Severity.Warning, fieldDeclaration, "Field does not exist in the record's layout."); }
        }

        public override void Translate(CodeWriter writer)
        {
            writer.Using("System.Runtime.InteropServices");

            //TODO
            using var _ = writer.DisableScope(String.IsNullOrEmpty(TranslatedName), File, Record, "Unimplemented translation: Anonymous record");

            writer.EnsureSeparation();
            //TODO: Documentation comment
            writer.WriteLine($"[StructLayout(LayoutKind.Explicit, Size = {Size})]");
            // Records are translated as ref structs to prevent storing them on the managed heap.
            writer.WriteLine($"public unsafe ref partial struct {SanitizeIdentifier(TranslatedName)}");
            using (writer.Block())
            {
                // Write out members
                foreach (TranslatedDeclaration member in _Members)
                { member.Translate(writer); }

#if false
                // Write out virtual methods
                if (layout->FirstVTable != null)
                {
                    PathogenVTable* vTable = layout->FirstVTable;

                    // Determine vTable entry names
                    string[] vTableEntryNames = new string[vTable->EntryCount];
                    {
                        bool foundFirstFunctionPointer = false;
                        for (int i = 0; i < vTable->EntryCount; i++)
                        {
                            PathogenVTableEntry* entry = &vTable->Entries[i];

                            if (entry->Kind.IsFunctionPointerKind())
                            { foundFirstFunctionPointer = true; }
                            else if (!foundFirstFunctionPointer)
                            {
                                // We skip all vtable entries until the first field since that's where the vTable pointer will actually point
                                //TODO: This should be accurate for both Itanium and Microsoft ABIs, but it'd be ideal if PathogenLayoutExtensions just gave this to us.
                                vTableEntryNames[i] = null;
                                continue;
                            }

                            if (entry->Kind == PathogenVTableEntryKind.FunctionPointer)
                            {
                                CXXMethodDecl method = (CXXMethodDecl)File.FindCursor(entry->MethodDeclaration);
                                vTableEntryNames[i] = $"{method.Name}_{i}";
                            }
                            else
                            { vTableEntryNames[i] = $"__{entry->Kind}_{i}"; }
                        }
                    }

                    // Write out virtual methods
                    //TODO

                    // Write out vTable type
                    {
                        writer.EnsureSeparation();
                        writer.WriteLine("[StructLayout(LayoutKind.Sequential)]");
                        writer.WriteLine($"public unsafe struct {SanitizeIdentifier(VTableTypeName)}");
                        using (writer.Block())
                        {
                            foreach (string fieldName in vTableEntryNames)
                            {
                                // Skip unnamed entries (these exist before the first entry where the vTable pointer points
                                if (fieldName == null)
                                { continue; }

                                //TODO: Use function pointer types in C#9
                                writer.WriteLine($"public void* {SanitizeIdentifier(fieldName)};");
                            }
                        }
                    }

                    // If there's additional vtables, emit warnings since we don't know how to translate them.
                    if (vTable->NextVTable != null)
                    {
                        File.Diagnostic(Severity.Warning, Record, $"Record {Record.Name} has more than one vtable, only the first vtable was translated.");

                        for (PathogenVTable* additionalVTable = vTable->NextVTable; additionalVTable != null; additionalVTable = additionalVTable->NextVTable)
                        {
                            for (int i = 0; i < additionalVTable->EntryCount; i++)
                            {
                                PathogenVTableEntry* entry = &additionalVTable->Entries[i];

                                if (!entry->Kind.IsFunctionPointerKind())
                                { continue; }

                                Cursor methodCursor = File.FindCursor(entry->MethodDeclaration);
                                File.Diagnostic(Severity.Warning, methodCursor, $"{Record.Name}::{methodCursor} will not be translated because it exists in a non-primary vtable.");
                            }
                        }
                    }
                }
#endif
            }

            // Mark the record as consumed
            File.Consume(Record);
        }

        private Dictionary<PathogenRecordFieldKind, int> UnnamedFieldCounts = null;
        internal string GetNameForUnnamedField(PathogenRecordFieldKind forKind)
        {
            if (UnnamedFieldCounts is null)
            { UnnamedFieldCounts = new Dictionary<PathogenRecordFieldKind, int>(); }

            int oldCount;

            if (!UnnamedFieldCounts.TryGetValue(forKind, out oldCount))
            { oldCount = 0; }

            UnnamedFieldCounts[forKind] = oldCount + 1;
            return $"__unnamed{forKind}{oldCount}";
        }
    }
}
