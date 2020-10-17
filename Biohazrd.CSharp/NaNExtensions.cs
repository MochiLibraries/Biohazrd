using System;

namespace Biohazrd.CSharp
{
    internal static class NaNExtensions
    {
        public static int GetBits(this float f)
            => BitConverter.SingleToInt32Bits(f);

        public static long GetBits(this double f)
            => BitConverter.DoubleToInt64Bits(f);

        private const int NaN32Bits = unchecked((int)0xFFC0_0000);
        private const int NaN64Bits = unchecked((int)0xFFF8_0000_0000_0000);

        public static bool IsUnusualNaN(this float f)
            => f.GetBits() != NaN32Bits;

        public static bool IsUnusualNaN(this double f)
            => f.GetBits() != NaN64Bits;
    }
}
