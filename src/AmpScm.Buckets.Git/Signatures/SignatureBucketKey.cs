using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;

namespace AmpScm.Buckets.Signatures
{
    public enum SignatureBucketAlgorithm
    {
        None,
        Rsa,
        Dsa,
        Ecdsa,
        Ed25519,
        Ecdh,
        Curve25519,
        Elgamal,
    }

    /// <summary>
    /// Public key for usage with <see cref="SignatureBucket"/>
    /// </summary>
    public sealed record class SignatureBucketKey
    {
        internal SignatureBucketKey(ReadOnlyMemory<byte> fingerprint, SignatureBucketAlgorithm algorithm, IReadOnlyList<ReadOnlyMemory<byte>> values, bool hasSecret)
        {
            Algorithm = algorithm;
            Values = values;
            Fingerprint = fingerprint;
            HasSecret = hasSecret;
        }

        public ReadOnlyMemory<byte> Fingerprint { get; }
        public SignatureBucketAlgorithm Algorithm { get; }
        public IReadOnlyList<ReadOnlyMemory<byte>> Values { get; }
        public string FingerprintString => SignatureBucket.FingerprintToString(Fingerprint);

        public bool HasSecret { get; }

        public static bool TryParse(string keyText, [NotNullWhen(true)] out SignatureBucketKey? value)
        {
            if (string.IsNullOrWhiteSpace(keyText))
                throw new ArgumentNullException(nameof(keyText));

            keyText = keyText.Trim();

            if (keyText.StartsWith("-----BEGIN ", StringComparison.Ordinal)
                || keyText.StartsWith("---- BEGIN ", StringComparison.Ordinal))
                return TryParseBlob(keyText, out value);
            else
                return TryParseSshLine(keyText, out value);
        }

        private static bool TryParseBlob(string line, out SignatureBucketKey? value)
        {
            var b = Bucket.Create.FromASCII(line);

            var r = new Radix64ArmorBucket(b);
            using var sig = new SignatureBucket(r);

            try
            {
                value = sig.ReadKeyAsync().AsTask().GetAwaiter().GetResult();
                return true;
            }
            catch (BucketException)
            {
                value = null;
                return false;
            }
        }

        internal static bool TryParseSshLine(string line, [NotNullWhen(true)] out SignatureBucketKey? value)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentNullException(nameof(line));

            line = line.Trim();

            var items = line.Split(new char[] { ' ' }, 3);
            var data = Convert.FromBase64String(items[1]);

            var vals = SignatureBucket.ParseSshStrings(data);

            var name = Encoding.ASCII.GetString(vals[0].ToArray());

            if (name != items[0])
            {
                value = null;
                return false;
            }

            var alg = items[0];

            if (alg.StartsWith("sk-", StringComparison.Ordinal))
                alg = alg.Substring(3);

            switch (alg)
            {
                case "ssh-rsa":
                    value = new SignatureBucketKey(data, SignatureBucketAlgorithm.Rsa,
                        new[]
                        {
                            vals[2],
                            vals[1],
                        },
                        false
                    );
                    return true;
                case "ssh-dss":
                    value = new SignatureBucketKey(data, SignatureBucketAlgorithm.Dsa,
                        new[]
                        {
                            vals[1],
                            vals[2],
                            vals[3],
                            vals[4],
                        },
                        false
                    );
                    return true;
                case "ssh-ed25519":
                    value = new SignatureBucketKey(data, SignatureBucketAlgorithm.Ed25519,
                        new[]
                        {
                            vals[1],
                        },
                        false);
                    return true;
                case "ecdsa-sha2-nistp256":
                case "ecdsa-sha2-nistp384":
                case "ecdsa-sha2-nistp521":
                    {
                        var signature = SignatureBucket.GetEcdsaValues(vals.Skip(1));
                        value = new SignatureBucketKey(data, SignatureBucketAlgorithm.Ecdsa, signature, false);
                        return true;
                    }
                default:
                    throw new NotImplementedException($"SSH public key format {items[0]} not implemented yet");
            }
        }
    }
}
