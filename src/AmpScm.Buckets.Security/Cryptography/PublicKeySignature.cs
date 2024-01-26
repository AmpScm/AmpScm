using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using AmpScm.Buckets;

namespace AmpScm.Buckets.Cryptography;

/// <summary>
/// Public key for usage with <see cref="SignatureBucket"/>
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public sealed record class PublicKeySignature : AsymmetricCryptoKey
{
    private readonly IEnumerable<AsymmetricCryptoKey>? _subKeys;

    internal PublicKeySignature(ReadOnlyMemory<byte> fingerprint, CryptoAlgorithm algorithm, IReadOnlyList<BigInteger> values, System.Net.Mail.MailAddress? mailAddress = null, bool hasSecret = false, IEnumerable<PublicKeySignature>? subKeys = null)
        : base(algorithm)
    {
        Values = values;
        InternalFingerprint = fingerprint;
        MailAddress = mailAddress;
        HasPrivateKey = hasSecret;
        _subKeys = subKeys;
    }

    // Helper for easy constructing
    internal PublicKeySignature WithSubKeys(IEnumerable<PublicKeySignature> enumerable, System.Net.Mail.MailAddress? mailAddress)
    {
        return new PublicKeySignature(InternalFingerprint, Algorithm, Values, mailAddress, HasPrivateKey, enumerable);
    }

    internal ReadOnlyMemory<byte> InternalFingerprint { get; }

    public override ReadOnlyMemory<byte> Fingerprint
    {
        get
        {
            var r = InternalFingerprint;
            if (r.Span[0] >= 3 && r.Span[0] <= 5)
                return InternalFingerprint.Slice(1);
            else
                return r;
        }
    }

    private IReadOnlyList<BigInteger> Values { get; }
    public string FingerprintString => CryptoDataBucket.FingerprintToString(InternalFingerprint);

    public System.Net.Mail.MailAddress? MailAddress { get; }

    public override IEnumerable<AsymmetricCryptoKey> SubKeys => _subKeys ?? Enumerable.Empty<PublicKeySignature>();


    public override bool MatchesFingerprint(ReadOnlyMemory<byte> fingerprint, CryptoAlgorithm algorithm = default, bool requirePrivateKey = false)
    {
        var a = MatchFingerprint(fingerprint, algorithm);

        if (a != null && (!requirePrivateKey || a.HasPrivateKey))
            return true;

        return false;
    }

    /// <summary>
    /// Returns a boolean indicating whether this key applies to the specified fingerprint (directly, or indirectly via subkeys)
    /// </summary>
    /// <param name="fingerprint"></param>
    /// <param name="algorithm"></param>
    /// <param name="requirePrivateKey"></param>
    /// <returns></returns>
    internal AsymmetricCryptoKey? MatchFingerprint(ReadOnlyMemory<byte> fingerprint, CryptoAlgorithm algorithm = default, bool requirePrivateKey = false)
    {
        int len = Math.Min(fingerprint.Length, Fingerprint.Length);

        if (fingerprint.Span.Slice(fingerprint.Length - len)
            .SequenceEqual(Fingerprint.Span.Slice(Fingerprint.Length - len))
            && (!requirePrivateKey || HasPrivateKey))
        {
            return this;
        }

        return _subKeys?.FirstOrDefault(x => x.MatchesFingerprint(fingerprint, algorithm) && (!requirePrivateKey || x.HasPrivateKey));
    }

    public static bool TryParse(string keyText, [NotNullWhen(true)] out PublicKeySignature? value)
    {
        return TryParse(keyText, getPassPhrase: null, out value);
    }

    public static bool TryParse(string keyText, Func<SignaturePromptContext, string>? getPassPhrase, [NotNullWhen(true)] out PublicKeySignature? value)
    {
        if (string.IsNullOrWhiteSpace(keyText))
            throw new ArgumentNullException(nameof(keyText));

        keyText = keyText.Trim();

        if (keyText.StartsWith("-----BEGIN ", StringComparison.Ordinal)
            || keyText.StartsWith("---- BEGIN ", StringComparison.Ordinal))
            return TryParseBlob(keyText, getPassPhrase, out value);
        else
            return TryParseSshLine(keyText, out value);
    }

    private static bool TryParseBlob(string line, Func<SignaturePromptContext, string>? getPassPhrase, out PublicKeySignature? value)
    {
        var b = Bucket.Create.FromASCII(line);

        var r = new Radix64ArmorBucket(b);
        using var sig = new SignatureBucket(r) { GetPassword = getPassPhrase };

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

    public override IReadOnlyList<BigInteger> GetValues(bool includePrivate = false)
    {
        return Values;
    }

    internal static bool TryParseSshLine(string line, [NotNullWhen(true)] out PublicKeySignature? value)
    {
        if (string.IsNullOrWhiteSpace(line))
            throw new ArgumentNullException(nameof(line));

        line = line.Trim();

        string[] items = line.Split(' ', 3);
        byte[] data = Convert.FromBase64String(items[1]);

        var vals = CryptoDataBucket.ParseSshStrings(data);

        string name = Encoding.ASCII.GetString(vals[0].ToCryptoValue());

        if (name != items[0])
        {
            value = null;
            return false;
        }

        string alg = items[0];

        if (alg.StartsWith("sk-", StringComparison.Ordinal))
            alg = alg.Substring(3);

        (CryptoAlgorithm Alg, BigInteger[] Vals) vv;

        switch (alg)
        {
            case "ssh-rsa":
                vv = (CryptoAlgorithm.Rsa,
                    new[]
                    {
                        vals[2],
                        vals[1],
                    });
                break;
            case "ssh-dss":
                vv = (CryptoAlgorithm.Dsa,
                    new[]
                    {
                        vals[1],
                        vals[2],
                        vals[3],
                        vals[4],
                    });
                break;
            case "ssh-ed25519":
                vv = (CryptoAlgorithm.Ed25519,
                    new[]
                    {
                        vals[1],
                    });
                break;
            case "ecdsa-sha2-nistp256":
            case "ecdsa-sha2-nistp384":
            case "ecdsa-sha2-nistp521":
                {
                    var values = CryptoDataBucket.GetEcdsaValues(vals.Skip(1).ToArray());
                    vv = (CryptoAlgorithm.Ecdsa, values);
                    break;
                }
            default:
                throw new NotSupportedException($"SSH public key format {items[0]} not implemented yet");
        }


        value = new PublicKeySignature(data /*CryptoDataBucket.CreateSshFingerprint(vv.Alg, vv.Vals)*/, vv.Alg, vv.Vals);

        //Debug.Assert(line.StartsWith(value.FingerprintString+" "), $"{value.FingerprintString} == {line}");

        return true;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
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
            for (int i = 0; i < span.Length; i++)
            {
                if (i < span.Length)
                    sb.Append(span[i].ToString("X2", CultureInfo.InvariantCulture));
                else
                    sb.Append("  ");

                sb.Append(' ');
            }

            if (HasPrivateKey)
                sb.Append(" - Contains Private Key");

            return sb.ToString();
        }
    }
}
