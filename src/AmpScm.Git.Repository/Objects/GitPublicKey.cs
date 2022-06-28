using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Signatures;

namespace AmpScm.Git.Objects
{
    public sealed class GitPublicKey
    {
        SignatureBucketKey _key;

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
            if (SignatureBucketKey.TryParse(value, out var v))
            {
                result = new(v);
                return true;
            }

            result = null;
            return false;
        }
    }
}
