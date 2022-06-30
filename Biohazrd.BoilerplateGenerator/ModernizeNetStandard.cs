using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        public NotNullWhenAttribute(bool returnValue)
            => ReturnValue = returnValue;
        public bool ReturnValue { get; }
    }

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class MaybeNullWhenAttribute : Attribute
    {
        public MaybeNullWhenAttribute(bool returnValue)
            => ReturnValue = returnValue;
        public bool ReturnValue { get; }
    }
}

namespace System.Collections.Generic
{
    internal static class SystemCollectionsGenericModernization
    {
        public static bool TryPeek<T>(this Stack<T> stack, [MaybeNullWhen(false)] out T result)
        {
            if (stack.Count > 0)
            {
                result = stack.Peek();
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        public static bool TryDequeue<T>(this Queue<T> queue, [MaybeNullWhen(false)] out T result)
        {
            if (queue.Count > 0)
            {
                result = queue.Dequeue();
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }
    }
}

namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    { }
}

internal static class StringModernization
{
    public static bool Contains(this string str, char c)
        => str.IndexOf(c) != -1;
}
