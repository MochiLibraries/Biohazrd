using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static ClangSharpTest2020.CodeWriter;

namespace ClangSharpTest2020
{
    public sealed class TranslatedVTable : TranslatedDeclaration
    {
        internal TranslatedVTableField VTableField { get; }
        private TranslatedRecord Record => VTableField.Record;

        public override string DefaultName => VTableField.TranslatedTypeName;
        public override bool CanBeRoot => false;

        private struct VTableEntry
        {
            public PathogenVTableEntry Info;
            public string Name;
        }

        private VTableEntry[] Entries;

        internal unsafe TranslatedVTable(TranslatedVTableField vTableField, PathogenVTable* vTable)
            : base(vTableField.Record)
        {
            VTableField = vTableField;
            Entries = new VTableEntry[vTable->EntryCount];

            // These are used to disambiguate names
            var firstUseOfName = new Dictionary<string, int>(); // name => index
            var countOfName = new Dictionary<string, int>(); // name => count

            // Iterate through each entry and assign it a name
            for (int i = 0; i < Entries.Length; i++)
            {
                PathogenVTableEntry info = vTable->Entries[i];

                // Determine the name for this entry
                string name;

                if (info.Kind == PathogenVTableEntryKind.FunctionPointer)
                { name = info.MethodDeclaration.Spelling.ToString(); }
                else
                { name = $"__{info.Kind}"; }

                // Disambiguate the name
                if (countOfName.TryGetValue(name, out int existingUses))
                {
                    // If this is the second use of that name rename the first use
                    if (existingUses == 1)
                    {
                        int firstUseIndex = firstUseOfName[name];
                        Entries[firstUseIndex].Name += "_0";
                    }

                    // Log the new use of that name and rename ourselves to avoid collision
                    countOfName[name] = existingUses + 1;
                    name += $"_{existingUses}";
                }
                // This is the first use of that name
                else
                {
                    firstUseOfName[name] = i;
                    countOfName[name] = 1;
                }

                // Record the entry
                Entries[i] = new VTableEntry()
                {
                    Info = info,
                    Name = name,
                };
            }
        }

        /// <summary>Returns the name of the vtable entry for the specified method or null if the method wasn't found.</summary>
        internal string GetVTableEntryNameForMethod(TranslatedFunction function)
        {
            foreach (VTableEntry entry in Entries)
            {
                if (entry.Info.MethodDeclaration == function.Function.Handle)
                { return entry.Name; }
            }

            return null;
        }

        protected override void TranslateImplementation(CodeWriter writer)
        {
            // Associate VTable entries with translated methods
            // We do this as late as possible to avoid weird behaviors when methods are removed (or maybe even added to) records.
            // Note that we don't bother erroring when a method has no corresponding slot since we assume it will complain when it can't find its slot.
            TranslatedFunction[] methods = new TranslatedFunction[Entries.Length];
            foreach (TranslatedFunction method in Record.Members.OfType<TranslatedFunction>().Where(f => f.IsVirtual))
            {
                // Associate the method
                for (int i = 0; i < Entries.Length; i++)
                {
                    // Only function pointer entries are applicable here
                    if (!Entries[i].Info.Kind.IsFunctionPointerKind())
                    { continue; }

                    // Check if this method matches
                    if (Entries[i].Info.MethodDeclaration == method.Function.Handle)
                    {
                        Debug.Assert(methods[i] is null, "Methods should not associate to the same vtable slot more than once.");
                        methods[i] = method;
                    }
                }
            }

            // Translate the vtable
            writer.EnsureSeparation();
            writer.WriteLine("[StructLayout(LayoutKind.Sequential)]");
            writer.WriteLine($"{Accessibility.ToCSharpKeyword()} unsafe struct {SanitizeIdentifier(TranslatedName)}");
            using (writer.Block())
            {
                bool foundFirstFunctionPointer = false;
                for (int i = 0; i < Entries.Length; i++)
                {
                    // If this entry is a function pointer kind, we can start writing
                    // (VTables have non-vtable stuff like RTTI before the vtable pointer, we don't want to translate those.)
                    if (Entries[i].Info.Kind.IsFunctionPointerKind())
                    { foundFirstFunctionPointer = true; }

                    // If we haven't found a function pointer yet, we don't want to start writing
                    if (!foundFirstFunctionPointer)
                    { continue; }

                    TranslatedFunction associatedMethod = methods[i];

                    // For function pointers, write out the signature of the method as a documentation comment
                    if (associatedMethod is object)
                    { writer.WriteLine($"/// <summary>Virtual method pointer for `{associatedMethod.Function.Handle.DisplayName}`</summary>"); }

                    writer.Write("public ");

                    // Write out the entry's type
                    // If we have an associated method, we write out the function pointer type. Otherwise, the entry is untyped.
                    if (associatedMethod is object)
                    { associatedMethod.TranslateFunctionPointerType(writer); }
                    else
                    { writer.Write("void*"); }

                    // Write out the entry's name
                    writer.WriteLine($" {SanitizeIdentifier(Entries[i].Name)};");
                }
            }
        }
    }
}
