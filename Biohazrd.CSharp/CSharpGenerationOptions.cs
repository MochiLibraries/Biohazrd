using ClangSharp.Pathogen;

namespace Biohazrd.CSharp
{
    public sealed record CSharpGenerationOptions
    {
        public bool DumpClangInfo { get; init; } = false;
        public ClangSharpInfoDumper.Options DumpOptions { get; init; }

        public bool HideTrampolinesFromDebugger { get; init; } = true;

        public CSharpGenerationOptions()
            => DumpOptions = ClangSharpInfoDumper.DefaultOptions;

        public static readonly CSharpGenerationOptions Default = new();
    }
}
