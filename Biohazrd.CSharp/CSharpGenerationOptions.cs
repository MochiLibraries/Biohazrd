using ClangSharp.Pathogen;

namespace Biohazrd.CSharp
{
    public sealed record CSharpGenerationOptions
    {
        public bool DumpClangInfo { get; init; }
        public ClangSharpInfoDumper.Options DumpOptions { get; init; }

        public bool HideTrampolinesFromDebugger { get; init; } = true;

        public CSharpGenerationOptions()
        {
#if DEBUG
            DumpClangInfo = true;
#endif
            DumpOptions = ClangSharpInfoDumper.DefaultOptions;
        }

        public static readonly CSharpGenerationOptions Default = new();
    }
}
