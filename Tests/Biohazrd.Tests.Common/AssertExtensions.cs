using System.Collections.Generic;

namespace Xunit
{
    partial class Assert
    {
        public static void ReferenceEqual<T>(T? expected, T? actual)
            where T : class
            => Equal<T>(expected, actual, ReferenceEqualityComparer.Instance);
    }
}
