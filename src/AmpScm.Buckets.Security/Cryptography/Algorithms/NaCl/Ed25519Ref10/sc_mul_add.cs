using System;

namespace Chaos.NaCl.Internal.Ed25519Ref10;

internal static partial class ScalarOperations
{
    private static long load_3(byte[] input, int offset)
    {
        long result;
        result = (long)input[offset + 0];
        result |= ((long)input[offset + 1]) << 8;
        result |= ((long)input[offset + 2]) << 16;
        return result;
    }

    private static long load_4(byte[] input, int offset)
    {
        long result;
        result = (long)input[offset + 0];
        result |= ((long)input[offset + 1]) << 8;
        result |= ((long)input[offset + 2]) << 16;
        result |= ((long)input[offset + 3]) << 24;
        return result;
    }

}
