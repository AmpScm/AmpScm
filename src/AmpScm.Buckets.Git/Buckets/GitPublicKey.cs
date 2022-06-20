using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Git
{
    public enum GitPublicKeyAlgorithm
    {
        None,
        Rsa,
        Dsa,
        Ecdsa,
        Ed25519,
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

        public static bool TryParse(string line, [NotNullWhen(true)] out GitPublicKey? value)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentNullException(nameof(line));

            line = line.Trim();
            var items = line.Split(new char[] { ' ' }, 3);
            var data = Convert.FromBase64String(items[1]);

            var vals = GitSignatureBucket.ParseSshStrings(data);

            switch (items[0])
            {
                case "ssh-rsa":
                    value = new GitPublicKey(Encoding.ASCII.GetBytes(items[2]), GitPublicKeyAlgorithm.Rsa,
                        new BigInteger[]
                        {
                            new BigInteger(vals[2].ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()),
                            new BigInteger(vals[1].ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()),
                        }
                    );
                    return true;
                case "ssh-dss":
                    value = new GitPublicKey(Encoding.ASCII.GetBytes(items[2]), GitPublicKeyAlgorithm.Dsa,
                        new BigInteger[]
                        {
                            new BigInteger(vals[1].ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()),
                            new BigInteger(vals[2].ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()),
                            new BigInteger(vals[3].ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()),
                            new BigInteger(vals[4].ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()),
                        }
                    );
                    return true;
                case "ssh-ed25519":
                    value = new GitPublicKey(Encoding.ASCII.GetBytes(items[2]), GitPublicKeyAlgorithm.Ed25519,
                        new BigInteger[]
                        {
                            new BigInteger(vals[1].ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()),
                            new BigInteger(vals[2].ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()),
                            new BigInteger(vals[3].ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()),
                            new BigInteger(vals[4].ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()),
                        });
                    return true;
                case "ecdsa-sha2-nistp256":
                    value = new GitPublicKey(Encoding.ASCII.GetBytes(items[2]), GitPublicKeyAlgorithm.Ecdsa,
                        new BigInteger[]
                        {
                            new BigInteger(vals[1].ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()),
                            new BigInteger(vals[2].ToArray().Reverse().Concat(new byte[] { 0 }).ToArray()),
                        }
                    );
                    return true;
                default:
                    throw new NotImplementedException();
            }
            return false;
        }
    }
}
