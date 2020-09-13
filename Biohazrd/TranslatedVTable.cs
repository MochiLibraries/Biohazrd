using ClangSharp.Interop;
using ClangSharp.Pathogen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Biohazrd
{
    public sealed record TranslatedVTable : TranslatedDeclaration
    {
        public ImmutableArray<TranslatedVTableEntry> Entries { get; init; }

        internal unsafe TranslatedVTable(TranslationUnitParser parsingContext, TranslatedFile file, PathogenVTable* vTable)
            : base(file)
        {
            Name = "VirtualMethodTable";

            ImmutableArray<TranslatedVTableEntry>.Builder entriesBuilder = ImmutableArray.CreateBuilder<TranslatedVTableEntry>(vTable->EntryCount);

            // These are used to disambiguate names
            Dictionary<string, int> firstUseOfName = new(); // name => index
            Dictionary<string, int> countOfName = new(); // name => count

            // Iterate through each entry and assign it a name
            for (int i = 0; i < Entries.Length; i++)
            {
                PathogenVTableEntry info = vTable->Entries[i];

                // Determine the name for this entry
                string name;

                if (info.Kind == PathogenVTableEntryKind.FunctionPointer)
                {
                    name = info.MethodDeclaration.Spelling.ToString();

                    if (info.MethodDeclaration.DeclKind == CX_DeclKind.CX_DeclKind_CXXMethod)
                    {
                        ref PathogenOperatorOverloadInfo operatorOverloadInfo = ref info.MethodDeclaration.GetOperatorOverloadInfo();

                        if (operatorOverloadInfo.Kind != PathogenOperatorOverloadKind.None)
                        { name = $"operator_{operatorOverloadInfo.Name}"; }
                    }
                    //TODO: Conversion operator overloads?
                }
                else
                { name = $"__{info.Kind}"; }

                // Disambiguate the name
                if (countOfName.TryGetValue(name, out int existingUses))
                {
                    // If this is the second use of that name rename the first use
                    if (existingUses == 1)
                    {
                        int firstUseIndex = firstUseOfName[name];
                        entriesBuilder[firstUseIndex] = entriesBuilder[firstUseIndex] with { Name = entriesBuilder[firstUseIndex].Name + "_0" };
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
                entriesBuilder.Add(new TranslatedVTableEntry(parsingContext, file, info, name));
            }

            Debug.Assert(entriesBuilder.Count == vTable->EntryCount);
            Entries = entriesBuilder.MoveToImmutable();
        }

        public override IEnumerator<TranslatedDeclaration> GetEnumerator()
        {
            foreach (TranslatedVTableEntry entry in Entries)
            { yield return entry; }
        }
    }
}
