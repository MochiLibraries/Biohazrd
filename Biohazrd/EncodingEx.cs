using System;
using System.Diagnostics;
using System.Text;

namespace Biohazrd
{
    internal static class EncodingEx
    {
        public static byte[] GetBytesNullTerminated(this Encoding encoding, string text)
        {
            byte[] ret = new byte[encoding.GetByteCount(text) + 1]; //PERF: Use GC.AllocateUninitializedArray

            int bytesWritten = encoding.GetBytes(text.AsSpan(), ret.AsSpan().Slice(0, ret.Length - 1));
            Debug.Assert(bytesWritten == (ret.Length - 1), "It's expected that the encoder uses the entire buffer sans null terminator.");

            ret[ret.Length - 1] = 0;

            return ret;
        }
    }
}
