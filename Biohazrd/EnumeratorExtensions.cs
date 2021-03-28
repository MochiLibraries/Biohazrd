using System.Collections.Generic;

namespace Biohazrd
{
    internal static class EnumeratorExtensions
    {
        public static IEnumerator<T> GetEnumerator<T>(this IEnumerator<T> enumerator)
            => enumerator;
    }
}
