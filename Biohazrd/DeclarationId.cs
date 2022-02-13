using System;
using System.Threading;

namespace Biohazrd
{
    public readonly struct DeclarationId : IEquatable<DeclarationId>
    {
        private readonly ulong Value;

        private DeclarationId(ulong value)
            => Value = value;

        public static bool operator ==(DeclarationId a, DeclarationId b)
            => a.Value == b.Value;

        public static bool operator !=(DeclarationId a, DeclarationId b)
            => a.Value != b.Value;

        public bool Equals(DeclarationId other)
            => this.Value == other.Value;

        public override bool Equals(object? obj)
            => obj is DeclarationId other ? Equals(other) : false;

        public override int GetHashCode()
            => Value.GetHashCode();

        public override string ToString()
            => $"Declaration{Value:X}";

        private static ulong NextId = 1;
        public static DeclarationId NewId()
        {
            ulong newId = Interlocked.Increment(ref NextId);
            return new DeclarationId(newId);
        }

        public static DeclarationId Null => default;
    }
}
