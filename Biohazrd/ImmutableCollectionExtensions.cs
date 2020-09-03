using System.Collections.Immutable;

namespace Biohazrd
{
    public static class ImmutableCollectionExtensions
    {
        public static ImmutableList<T> AddIfNotNull<T>(this ImmutableList<T> list, T? value)
            where T : class
            => value is not null ? list.Add(value) : list;

        public static ImmutableArray<T> AddIfNotNull<T>(this ImmutableArray<T> array, T? value)
            where T : class
            => value is not null ? array.Add(value) : array;
    }
}
