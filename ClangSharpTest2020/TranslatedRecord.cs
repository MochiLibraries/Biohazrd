#define VERBOSE_MODE
using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ClangSharpTest2020
{
    public sealed class TranslatedRecord
    {
        public ImmutableArray<TranslationContext> Context { get; }
        public TranslatedFile File { get; }
        public TranslatedRecord ParentRecord { get; }
        public RecordDecl Record { get; }

        private List<TranslatedFunction> Methods = new List<TranslatedFunction>();

        private TranslatedRecord(ImmutableArray<TranslationContext> context, TranslatedFile file, TranslatedRecord parentRecord, RecordDecl record)
        {
            if (!record.Handle.IsDefinition)
            { throw new ArgumentException("Only defining records can be translated!"); }

            Context = context;
            File = file;
            ParentRecord = parentRecord;
            Record = record;

            // Handle nested cursors
            ImmutableArray<TranslationContext> nestedContext = Context.Add(Record);

            // Add methods
            if (Record is CXXRecordDecl cxxRecord)
            {
                foreach (CXXMethodDecl method in cxxRecord.Methods)
                { Methods.Add(new TranslatedFunction(nestedContext, this, method)); }
            }

            //TODO: Add nested types

            // Process any other nested cursors which aren't fields or methods
            foreach (Cursor cursor in Record.CursorChildren)
            {
                // Fields are processed on-demand when we get the record layout
                if (cursor is FieldDecl)
                { continue; }

                // Methods were processed above
                if (cursor is FunctionDecl)
                { continue; }

                // Base specifiers are processed on-demand when we get the record layout
                //TODO: Consume the cursor?
                if (cursor is CXXBaseSpecifier)
                { continue; }

                // Access specifiers do not have a direct impact on the transaltion
                // The information they provide is available on the individual members
                if (cursor is AccessSpecDecl)
                {
                    file.Ignore(cursor);
                    continue;
                }

                // Skip anything that is an attribute
                // (Don't ignore/consume them though, there's logic in TranslatedFile to ignore attributes that don't affect output.)
                if (cursor is Attr)
                { continue; }

                file.ProcessCursor(nestedContext, cursor);
            }
        }

        internal TranslatedRecord(ImmutableArray<TranslationContext> context, TranslatedFile file, RecordDecl record)
            : this(context, file, null, record)
        { }

        internal TranslatedRecord(ImmutableArray<TranslationContext> context, TranslatedRecord parentRecord, RecordDecl record)
            : this(context, parentRecord.File, parentRecord, record)
        { }

        public void AddAsStaticMethod(TranslatedFunction translatedFunction)
        {
            if (translatedFunction.File != File)
            { throw new ArgumentException("The global function and the record must come from the same file.", nameof(translatedFunction)); }

            Methods.Add(new TranslatedFunction(Context.Add(Record), File, translatedFunction.Function));
        }

        private unsafe void Translate(CodeWriter writer, PathogenRecordLayout* layout)
        {
            //TODO: Use context to emit namespace and enclosing types.
            writer.Using("System.Runtime.InteropServices");
            writer.EnsureSeparation();
            //TODO: Documentation comment
            writer.WriteLine($"[StructLayout(LayoutKind.Explicit, Size = {layout->Size})]");
            // Records are translated as ref structs to prevent storing them on the managed heap.
            writer.WriteLine($"public unsafe ref partial struct {Record.Name}");
            using (writer.Block())
            {
                const string VTableTypeName = "VirtualMethodTable";
                const string VTableFieldName = "VirtualMethodTablePointer";

                // Write out fields
                for (PathogenRecordField* field = layout->FirstField; field != null; field = field->NextField)
                {
                    bool isBitField = field->Kind == PathogenRecordFieldKind.Normal && field->IsBitField != 0;
                    bool isNonPrimaryBase = (field->Kind == PathogenRecordFieldKind.VirtualBase || field->Kind == PathogenRecordFieldKind.NonVirtualBase) && field->IsPrimaryBase == 0;
                    bool isUnsupportedKind = false;

                    CXCursor diagnosticCursor = field->Kind == PathogenRecordFieldKind.Normal ? field->FieldDeclaration : Record.Handle;

                    writer.EnsureSeparation();
                    //TODO: Documentation comment

                    // We might not be properly translating these field kinds
                    switch (field->Kind)
                    {
                        case PathogenRecordFieldKind.VirtualBase:
                        case PathogenRecordFieldKind.VirtualBaseTablePtr:
                        case PathogenRecordFieldKind.VTorDisp:
                            File.Diagnostic(Severity.Warning, diagnosticCursor, $"{field->Kind} fields may not be translated correctly.");
                            isUnsupportedKind = true;
                            break;
                    }

                    if (!isUnsupportedKind)
                    {
                        if (isBitField)
                        { File.Diagnostic(Severity.Warning, diagnosticCursor, $"Bitfields are very lazily translated, consider improving."); }

                        //TODO: For some reason this can happen with some records with only one base. Not sure why.
                        // This warning is really only of concern when the base is virtual, add a check for that here.
                        //if (isNonPrimaryBase)
                        //{ File.Diagnostic(TranslationDiagnosticSeverity.Warning, diagnosticCursor, $"Non-primary {field->Kind} fields may not be translated correctly."); }
                    }

#if VERBOSE_MODE
                    // Emit verbose field information
                    writer.Write($"// {field->Kind}");

                    if (isNonPrimaryBase)
                    { writer.Write("(NonPrimary)"); }

                    writer.Write($" {field->Type} {field->Name} @ {field->Offset}");

                    if (isBitField)
                    { writer.Write($"[{field->BitFieldStart}..{field->BitFieldStart + field->BitFieldWidth}]"); }

                    writer.WriteLine();
#endif

                    // Emit the field
                    {
                        // Field offset
                        writer.WriteLine($"[FieldOffset({field->Offset})]");

                        // Field access
                        //TODO: Skip private fields and mark protected fields internal.
                        string accessModifier = field->Kind == PathogenRecordFieldKind.Normal ? "public" : "internal";
                        writer.Write($"{accessModifier} ");

                        // Field type
                        if (field->Kind == PathogenRecordFieldKind.VTablePtr)
                        { writer.Write($"{VTableTypeName}*"); }
                        else
                        { writer.Write("void*"); } //TODO: Need to translate type.

                        writer.Write(' ');

                        // Field name
                        if (field->Kind == PathogenRecordFieldKind.VTablePtr)
                        { writer.Write(VTableFieldName); }
                        else
                        { writer.Write(field->Name.ToString()); }

                        // Done.
                        writer.WriteLine(';');

                        // Bitfield constants
                        if (isBitField)
                        {
                            writer.WriteLine();
                            writer.WriteLine($"{accessModifier} const int {field->Name}Shift = {field->BitFieldStart};");

                            writer.Write($"{accessModifier} const int {field->Name}Mask = 0b");
                            for (int i = 0; i < field->BitFieldWidth; i++)
                            { writer.Write('1'); }
                            writer.WriteLine(";");
                        }

                        //TODO: If the field is a non-virtual base that corresponds to a virtual class, add an alias property for the vTable that uses our specific vTable type.
                    }

                    // Mark the field as consumed
                    //TODO: This might be overzealous. Also we aren't consuming any default value stuff, should we?
                    if (field->Kind == PathogenRecordFieldKind.Normal)
                    {
                        //TODO: This can happen with anonyomous unions, need to figure out how to handle this appropriately.
                        if (field->FieldDeclaration.IsNull)
                        { File.Diagnostic(Severity.Warning, Record, $"{Record.Name} contains a field with no definition."); }
                        else
                        //TODO: This causes the type reference to be consumed multiple times when multiple fields are declared on the same line.
                        { File.ConsumeRecursive(field->FieldDeclaration); }
                    }
                }

                // Write out non-virtual methods
                foreach (TranslatedFunction function in Methods)
                {
                    //TODO: Ensure non-virtual
                    function.Translate(writer);
                }

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
                        writer.WriteLine($"public unsafe struct {VTableTypeName}");
                        using (writer.Block())
                        {
                            foreach (string fieldName in vTableEntryNames)
                            {
                                // Skip unnamed entries (these exist before the first entry where the vTable pointer points
                                if (fieldName == null)
                                { continue; }

                                //TODO: Use function pointer types in C#9
                                writer.WriteLine($"public void* {fieldName};");
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
            }

            // Mark the record as consumed
            File.Consume(Record);
        }

        public unsafe void Translate(CodeWriter writer)
        {
            PathogenRecordLayout* layout = null;
            try
            {
                layout = PathogenExtensions.pathogen_GetRecordLayout(Record.Handle);

                if (layout == null)
                {
                    File.Diagnostic(Severity.Fatal, Record, $"Failed top get the record layout of {Record.Name}");
                    return;
                }

                Translate(writer, layout);
            }
            finally
            {
                if (layout != null)
                { PathogenExtensions.pathogen_DeleteRecordLayout(layout); }
            }
        }

        public void Translate()
        {
            using CodeWriter writer = new CodeWriter();
            Translate(writer);
            //TODO: Use context to add enclosing types to file name.
            writer.WriteOut($"{Record.Name}.cs");
        }

        public override string ToString()
            => Record.Name;
    }
}
