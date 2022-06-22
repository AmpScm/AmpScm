using System;

namespace Chaos.NaCl.Internal
{
    // Array8<UInt32> Poly1305 key
    // Array8<UInt64> SHA-512 state/output
    internal struct Array8
    {
        public ulong x0;
        public ulong x1;
        public ulong x2;
        public ulong x3;
        public ulong x4;
        public ulong x5;
        public ulong x6;
        public ulong x7;
    }
}
