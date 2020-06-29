namespace ClangSharpTest2020
{
    internal static class GlobalConfiguration
    {
        public static readonly bool DumpClangDetails = false;
        /// <summary>If true, each type of Clang location will be included in the detail dump.</summary>
        /// <remarks>Requires <see cref="DumpClangDetails"/></remarks>
        public static readonly bool IncludeAllLocationDataInDump = false;
        /// <remarks>Requires <see cref="DumpClangDetails"/></remarks>
        public static readonly bool IncludeClangTypeDetailsInDump = false;
        /// <remarks>Requires <see cref="IncludeClangTypeDetailsInDump"/></remarks>
        public static readonly bool DumpClangTypeDetailsRecursively = true;
    }
}
