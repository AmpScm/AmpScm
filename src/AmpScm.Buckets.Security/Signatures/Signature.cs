using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;

namespace AmpScm.Buckets.Signatures
{
    public enum SignatureAlgorithm
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
    [DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
    public sealed record class Signature
    {
        private readonly IEnumerable<Signature>? _subKeys;

        internal Signature(ReadOnlyMemory<byte> fingerprint, SignatureAlgorithm algorithm, IReadOnlyList<ReadOnlyMemory<byte>> values, System.Net.Mail.MailAddress? mailAddress = null, bool hasSecret = false, IEnumerable<Signature>? subKeys = null)
        {
            Algorithm = algorithm;
            Values = values;
            Fingerprint = fingerprint;
            MailAddress = mailAddress;
            HasSecret = hasSecret;
            _subKeys = subKeys;
        }

        // Helper for easy constructing
        internal Signature WithSubKeys(IEnumerable<Signature> enumerable, System.Net.Mail.MailAddress? mailAddress)
        {
            return new Signature(Fingerprint, Algorithm, Values, mailAddress, HasSecret, enumerable);
        }

        public ReadOnlyMemory<byte> Fingerprint { get; }
        public SignatureAlgorithm Algorithm { get; }
        public IReadOnlyList<ReadOnlyMemory<byte>> Values { get; }
        public string FingerprintString => SignatureBucket.FingerprintToString(Fingerprint);

        public bool HasSecret { get; }

        public System.Net.Mail.MailAddress? MailAddress { get; }

        public IEnumerable<Signature> SubKeys => _subKeys ?? Enumerable.Empty<Signature>();


        /// <summary>
        /// Returns a boolean indicating whether this key applies to the specified fingerprint (directly, or indirectly via subkeys)
        /// </summary>
        /// <param name="fingerprint"></param>
        /// <returns></returns>
        public Signature? MatchFingerprint(ReadOnlyMemory<byte> fingerprint)
        {
            int len = Math.Min(fingerprint.Length, fingerprint.Length);

            if (fingerprint.Span.Slice(fingerprint.Length - len)
                .SequenceEqual(Fingerprint.Span.Slice(Fingerprint.Length - len)))
            {
                return this;
            }

            return _subKeys?.Select(x => x.MatchFingerprint(fingerprint)).FirstOrDefault(x => x is { });
        }

        public static bool TryParse(string keyText, [NotNullWhen(true)] out Signature? value)
        {
            return TryParse(keyText, null, out value);
        }

        public static bool TryParse(string keyText, Func<SignaturePromptContext, string>? getPassword, [NotNullWhen(true)] out Signature? value)
        {
            if (string.IsNullOrWhiteSpace(keyText))
                throw new ArgumentNullException(nameof(keyText));

            keyText = keyText.Trim();

            if (keyText.StartsWith("-----BEGIN ", StringComparison.Ordinal)
                || keyText.StartsWith("---- BEGIN ", StringComparison.Ordinal))
                return TryParseBlob(keyText, getPassword, out value);
            else
                return TryParseSshLine(keyText, out value);
        }

        private static bool TryParseBlob(string line, Func<SignaturePromptContext, string>? getPassword, out Signature? value)
        {
            var b = Bucket.Create.FromASCII(line);

            var r = new Radix64ArmorBucket(b);
            using var sig = new SignatureBucket(r) { GetPassword = getPassword };

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

        internal static bool TryParseSshLine(string line, [NotNullWhen(true)] out Signature? value)
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
                    value = new Signature(data, SignatureAlgorithm.Rsa,
                        new[]
                        {
                            vals[2],
                            vals[1],
                        }
                    );
                    return true;
                case "ssh-dss":
                    value = new Signature(data, SignatureAlgorithm.Dsa,
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
                    value = new Signature(data, SignatureAlgorithm.Ed25519,
                        new[]
                        {
                            vals[1],
                        });
                    return true;
                case "ecdsa-sha2-nistp256":
                case "ecdsa-sha2-nistp384":
                case "ecdsa-sha2-nistp521":
                    {
                        var signature = SignatureBucket.GetEcdsaValues(vals.Skip(1));
                        value = new Signature(data, SignatureAlgorithm.Ecdsa, signature);
                        return true;
                    }
                default:
                    throw new NotImplementedException($"SSH public key format {items[0]} not implemented yet");
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string DebuggerDisplay
        {
            get
            {
                StringBuilder sb = new StringBuilder(100);

                if (MailAddress is { })
                {
                    sb.Append(MailAddress);
                    sb.Append(" - ");
                }

                sb.AppendFormat(CultureInfo.InvariantCulture, "{0} ", Algorithm);

                var span = Fingerprint.Span;
                for (int i = 0; i < 16; i++)
                {
                    if (i < span.Length)
                        sb.Append(span[i].ToString("X2", CultureInfo.InvariantCulture));
                    else
                        sb.Append("  ");

                    sb.Append(' ');
                }

                if (HasSecret)
                    sb.Append(" - Contains Private Key");

                return sb.ToString();
            }
        }
    }
}
