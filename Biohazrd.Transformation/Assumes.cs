using System.Diagnostics;

namespace Biohazrd.Transformation
{
    // This is a placeholder for a future Roslyn analyzer opporunity
    internal static class Assumes
    {
        [Conditional("__NEVER_DEFINE_THIS")]
        public static void IsSealed<T>()
        { }
    }
}
