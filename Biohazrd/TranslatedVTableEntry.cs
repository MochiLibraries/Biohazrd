using ClangSharp.Pathogen;

namespace Biohazrd
{
    public sealed record TranslatedVTableEntry
    {
        public PathogenVTableEntry Info { get; }
        public string Name { get; init; }

        internal TranslatedVTableEntry(PathogenVTableEntry info, string name)
        {
            Info = info;
            Name = name;
        }
    }
}
