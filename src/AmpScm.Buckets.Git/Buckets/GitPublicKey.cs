using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
        EdDsa,
    }

    public sealed record class GitPublicKey
    {
        public GitPublicKey(IReadOnlyList<byte> fingerprint, GitPublicKeyAlgorithm algorithm, IReadOnlyList<ReadOnlyMemory<byte>> values)
        {
            Algorithm = algorithm;
            Values = values;
            Fingerprint = fingerprint;
        }

        public IReadOnlyList<byte> Fingerprint { get; }
        public GitPublicKeyAlgorithm Algorithm { get; }
        public IReadOnlyList<ReadOnlyMemory<byte>> Values { get; }
        public string FingerprintString => GitSignatureBucket.FingerprintToString(Fingerprint);

        public static bool TryParse(string line, [NotNullWhen(true)] out GitPublicKey? value)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentNullException(nameof(line));

            line = line.Trim();
            var items = line.Split(new char[] { ' ' }, 3);
            var data = Convert.FromBase64String(items[1]);

            var vals = GitSignatureBucket.ParseSshStrings(data);

            var name = Encoding.ASCII.GetString(vals[0].ToArray());

            if (name != items[0])
            {
                value = null;
                return false;
            }

            switch (items[0])
            {
                case "ssh-rsa":
                    value = new GitPublicKey(data, GitPublicKeyAlgorithm.Rsa,
                        new[]
                        {
                            vals[2],
                            vals[1],
                        }
                    );
                    return true;
                case "ssh-dss":
                    value = new GitPublicKey(data, GitPublicKeyAlgorithm.Dsa,
                        new[]
                        {
                            vals[1],
                            vals[2],
                            vals[3],
                            vals[4],
                        }
                    );
                    return true;
                case "ssh-ed25519":
                    value = new GitPublicKey(data, GitPublicKeyAlgorithm.Ed25519,
                        new[]
                        {
                            vals[1],
                        });
                    return true;
                case "ecdsa-sha2-nistp256":
                case "ecdsa-sha2-nistp384":
                case "ecdsa-sha2-nistp521":
                    {
                        var signature = GitSignatureBucket.GetEcdsaValues(vals.Skip(1));
                        value = new GitPublicKey(data, GitPublicKeyAlgorithm.Ecdsa, signature);
                        return true;
                    }
                default:
                    throw new NotImplementedException($"SSH public key format {items[0]} not implemented yet");
            }
        }
    }
}
