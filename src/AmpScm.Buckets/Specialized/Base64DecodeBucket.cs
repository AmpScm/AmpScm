using System;
using System.Linq;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class Base64DecodeBucket : ConversionBucket
    {
        byte[]? buffer;
        uint bits;
        int state;

        public Base64DecodeBucket(Bucket bucket)
            : base(bucket)
        {
        }

        static readonly sbyte[] base64Reversemap = new sbyte[]
        {
            /* '\x0': */   -3, /* '\x1': */   -3, /* '\x2': */   -3, /* '\x3': */   -3,
            /* '\x4': */   -3, /* '\x5': */   -3, /* '\x6': */   -3, /* '\x7': */   -3,
            /* '\x8': */   -3, /* '\x9': */   -3, /* '\xa': */   -1, /* '\xb': */   -3,
            /* '\xc': */   -3, /* '\xd': */   -1, /* '\xe': */   -3, /* '\xf': */   -3,
            /* '\x10': */  -3, /* '\x11': */  -3, /* '\x12': */  -3, /* '\x13': */  -3,
            /* '\x14': */  -3, /* '\x15': */  -3, /* '\x16': */  -3, /* '\x17': */  -3,
            /* '\x18': */  -3, /* '\x19': */  -3, /* '\x1a': */  -3, /* '\x1b': */  -3,
            /* '\x1c': */  -3, /* '\x1d': */  -3, /* '\x1e': */  -3, /* '\x1f': */  -3,
            /* ' ': */     -3, /* '!': */     -3, /* '"': */     -3, /* '#': */     -3,
            /* '$': */     -3, /* '%': */     -3, /* '&': */     -3, /* ''': */     -3,
            /* '(': */     -3, /* ')': */     -3, /* '*': */     -3, /* '+': */     62,
            /* ',': */     -3, /* '-': */     -3, /* '.': */     -3, /* '/': */     63,
            /* '0': */     52, /* '1': */     53, /* '2': */     54, /* '3': */     55,
            /* '4': */     56, /* '5': */     57, /* '6': */     58, /* '7': */     59,
            /* '8': */     60, /* '9': */     61, /* ':': */     -3, /* ';': */     -3,
            /* '<': */     -3, /* '=': */     -1, /* '>': */     -3, /* '?': */     -3,
            /* '@': */     -3, /* 'A': */      0, /* 'B': */      1, /* 'C': */      2,
            /* 'D': */      3, /* 'E': */      4, /* 'F': */      5, /* 'G': */      6,
            /* 'H': */      7, /* 'I': */      8, /* 'J': */      9, /* 'K': */     10,
            /* 'L': */     11, /* 'M': */     12, /* 'N': */     13, /* 'O': */     14,
            /* 'P': */     15, /* 'Q': */     16, /* 'R': */     17, /* 'S': */     18,
            /* 'T': */     19, /* 'U': */     20, /* 'V': */     21, /* 'W': */     22,
            /* 'X': */     23, /* 'Y': */     24, /* 'Z': */     25, /* '[': */     -3,
            /* '\': */     -3, /* ']': */     -3, /* '^': */     -3, /* '_': */     -3,
            /* '`': */     -3, /* 'a': */     26, /* 'b': */     27, /* 'c': */     28,
            /* 'd': */     29, /* 'e': */     30, /* 'f': */     31, /* 'g': */     32,
            /* 'h': */     33, /* 'i': */     34, /* 'j': */     35, /* 'k': */     36,
            /* 'l': */     37, /* 'm': */     38, /* 'n': */     39, /* 'o': */     40,
            /* 'p': */     41, /* 'q': */     42, /* 'r': */     43, /* 's': */     44,
            /* 't': */     45, /* 'u': */     46, /* 'v': */     47, /* 'w': */     48,
            /* 'x': */     49, /* 'y': */     50, /* 'z': */     51, /* '{': */     -3,
        };

        protected override BucketBytes ConvertData(ref BucketBytes sourceData, bool final)
        {
            buffer ??= new byte[1024];
            int i;
            int nb = 0;

            for (i = 0; i < sourceData.Length && nb + 4 < buffer.Length; i++)
            {
                byte b = sourceData[i];

                sbyte sb = (b < base64Reversemap.Length) ? base64Reversemap[b] : (sbyte)-3;

                if (sb < 0)
                {
                    if (sb == -1)
                        continue; // Newlines and '=' are skipped
                    else
                        throw new BucketException($"Unexpected base64 character 0x{b:x} '{(char)b}' in {Name} bucket");
                }

                bits |= (uint)sb << (6 * (3 - state));

                if (state < 3)
                {
                    state++;
                    continue;
                }
                state = 0;

                for (int d = 2; d >= 0; d--)
                {
                    buffer[nb++] = (byte)((bits >> 8 * d) & 0xFF);
                }
                bits = 0;
            }

            if (final && state > 0)
            {
                for (int d = 0; d <= 2; d++)
                {
                    if (d + 1 < state)
                        buffer[nb++] = (byte)((bits >> 16 - 8 * d) & 0xFF);
                }
                state = 0;
            }

            if (i > 0) // Pass EOF through unchanged
                sourceData = sourceData.Slice(i);

            return new BucketBytes(buffer, 0, nb);
        }

        protected override int ConvertRequested(int requested)
        {
            if (requested < 1024)
                return requested * 4 / 3;

            return requested;
        }
    }
}
