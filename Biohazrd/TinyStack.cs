using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Biohazrd
{
    /// <summary>Represents a stack which will not allocate until it surpases 10 elements.</summary>
    /// <remarks>This type is designed for contexts where a a short-lived stack will generally not have many elements.</remarks>
    internal struct TinyStack<T>
    {
        public int Count { get; private set; }
        [AllowNull] private T _0;
        [AllowNull] private T _1;
        [AllowNull] private T _2;
        [AllowNull] private T _3;
        [AllowNull] private T _4;
        [AllowNull] private T _5;
        [AllowNull] private T _6;
        [AllowNull] private T _7;
        [AllowNull] private T _8;
        [AllowNull] private T _9;
        private ImmutableStack<T> Overflow;

        public T Peek()
            => Count switch
            {
                0 => throw new InvalidOperationException("The stack is empty."),
                1 => _0,
                2 => _1,
                3 => _2,
                4 => _3,
                5 => _4,
                6 => _5,
                7 => _6,
                8 => _7,
                9 => _8,
                10 => _9,
                _ => Overflow.Peek()
            };

        public T Pop()
        {
            if (Count <= 10)
            {
                // Peek will throw for us if this stack is empty
                T ret = Peek();

                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                {
                    switch (Count)
                    {
                        case 1:
                            _0 = default;
                            break;
                        case 2:
                            _1 = default;
                            break;
                        case 3:
                            _2 = default;
                            break;
                        case 4:
                            _3 = default;
                            break;
                        case 5:
                            _4 = default;
                            break;
                        case 6:
                            _5 = default;
                            break;
                        case 7:
                            _6 = default;
                            break;
                        case 8:
                            _7 = default;
                            break;
                        case 9:
                            _8 = default;
                            break;
                        case 10:
                            _9 = default;
                            break;
                    }
                }

                Count--;
                return ret;
            }
            else
            {
                T ret;
                Overflow = Overflow.Pop(out ret);
                Count--;
                return ret;
            }
        }

        public void Push(T value)
        {
            switch (Count)
            {
                case 0:
                    _0 = value;
                    break;
                case 1:
                    _1 = value;
                    break;
                case 2:
                    _2 = value;
                    break;
                case 3:
                    _3 = value;
                    break;
                case 4:
                    _4 = value;
                    break;
                case 5:
                    _5 = value;
                    break;
                case 6:
                    _6 = value;
                    break;
                case 7:
                    _7 = value;
                    break;
                case 8:
                    _8 = value;
                    break;
                case 9:
                    _9 = value;
                    break;
                default:
                    if (Overflow is null)
                    { Overflow = ImmutableStack.Create(value); }
                    else
                    { Overflow = Overflow.Push(value); }
                    break;
            }

            Count++;
        }
    }
}
