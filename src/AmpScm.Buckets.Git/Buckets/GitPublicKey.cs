using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Git
{
    public enum GitPublicKeyAlgorithm
    {
        None,
        RSA,
    }

    public sealed record class GitPublicKey
    {
        public GitPublicKey(IReadOnlyList<byte> fingerprint, GitPublicKeyAlgorithm algorithm, IReadOnlyList<BigInteger> values)
        {
            Algorithm = algorithm;
            Values = values;
            Fingerprint = fingerprint;
        }

        public IReadOnlyList<byte> Fingerprint { get; }
        public GitPublicKeyAlgorithm Algorithm { get; }
        public IReadOnlyList<BigInteger> Values { get; }        
    }
}
