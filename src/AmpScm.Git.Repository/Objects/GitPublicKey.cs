using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Signatures;

namespace AmpScm.Git.Objects
{
    public sealed class GitPublicKey
    {
        SignatureBucketKey _key;
        ReadOnlyMemory<byte> _fingerprint;

        public string? Principal { get; init; }

        internal GitPublicKey(SignatureBucketKey key)
        {
            _key = key;
        }


        [return: NotNullIfNotNull("k")]
        public static implicit operator SignatureBucketKey?(GitPublicKey k) => k?._key;

        public SignatureBucketKey ToSignatureBucketKey()
        {
            return _key;
        }

        public static bool TryParse(string value, [NotNullWhen(true)] out GitPublicKey? result)
        {
            return TryParse(value, out result, principal: null);
        }

        internal static bool TryParse(string value, [NotNullWhen(true)] out GitPublicKey? result, string? principal = null)
        {
            if (SignatureBucketKey.TryParse(value, out var v))
            {
                result = new(v) { Principal = principal };
                return true;
            }

            result = null;
            return false;
        }

        public ReadOnlyMemory<byte> Fingerprint
        {
            get
            {
                if (_fingerprint.Length == 0)
                    _fingerprint = HashPrint(_key.Fingerprint);

                return _fingerprint;
            }
        }

        internal static ReadOnlyMemory<byte> HashPrint(ReadOnlyMemory<byte> fp)
        {
            if (fp.Length == 8) // 8 = PGP fingerprint
                return fp;
            else if (fp.Length <= (32 /* SHA256 */ + 1) && (fp.Length & 3) == 1 && fp.Span[0] >= 3 && fp.Span[0] <= 5)
                return fp.Slice(1);

#if NET6_0_OR_GREATER
            return SHA256.HashData(fp.Span);
#else
            using var sha = SHA256.Create();

            return sha.ComputeHash(fp.ToArray());
#endif
        }
    }
}
