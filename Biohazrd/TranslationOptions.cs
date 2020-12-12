namespace Biohazrd
{
    public record TranslationOptions
    {
        /// <summary>If true (the default), Biohazrd will never process declarations from system headers.</summary>
        /// <remarks>
        /// "System headers" uses the definition Clang uses for system headers: https://clang.llvm.org/docs/UsersManual.html#controlling-diagnostics-in-system-headers
        /// 
        /// Generally you should leave this enabled unless you're intentionally processing system headers with Biohazrd.
        /// </remarks>
        public bool SystemHeadersAreAlwaysOutOfScope { get; init; } = true;
    }
}
