using System;
using System.Collections;
using System.Collections.Generic;

namespace Biohazrd
{
    /// <summary>An enumerator which yields no elements.</summary>
    internal sealed class EmptyEnumerator<T> : IEnumerator<T>
    {
        T IEnumerator<T>.Current => default!;
        object IEnumerator.Current => null!;

        bool IEnumerator.MoveNext()
            => false;

        void IEnumerator.Reset()
        { }

        void IDisposable.Dispose()
        { }

        private EmptyEnumerator()
        { }

        /// <summary>A static, sharted instance of an enumerator of <typeparamref name="T"/> that yields no elements.</summary>
        public static readonly EmptyEnumerator<T> Instance = new EmptyEnumerator<T>();
    }
}
