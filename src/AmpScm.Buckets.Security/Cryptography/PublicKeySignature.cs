﻿using System;
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

namespace AmpScm.Buckets.Cryptography;

/// <summary>
/// Public key for usage with <see cref="SignatureBucket"/>
/// </summary>
[DebuggerDisplay($"{{{nameof(DebuggerDisplay)},nq}}")]
public sealed record class PublicKeySignature : AsymetricKey
{
    private readonly IEnumerable<AsymetricKey>? _subKeys;

    internal PublicKeySignature(ReadOnlyMemory<byte> fingerprint, CryptoAlgorithm algorithm, IReadOnlyList<BigInteger> values, System.Net.Mail.MailAddress? mailAddress = null, bool hasSecret = false, IEnumerable<PublicKeySignature>? subKeys = null)
        : base(algorithm)
    {
        Values = values;
        Fingerprint = fingerprint;
        MailAddress = mailAddress;
        HasPrivateKey = hasSecret;
        _subKeys = subKeys;
    }

    // Helper for easy constructing
    internal PublicKeySignature WithSubKeys(IEnumerable<PublicKeySignature> enumerable, System.Net.Mail.MailAddress? mailAddress)
    {
        return new PublicKeySignature(Fingerprint, Algorithm, Values, mailAddress, HasPrivateKey, enumerable);
    }

    public ReadOnlyMemory<byte> Fingerprint { get; }
    IReadOnlyList<BigInteger> Values { get; }
    public string FingerprintString => CryptoDataBucket.FingerprintToString(Fingerprint);

    public System.Net.Mail.MailAddress? MailAddress { get; }

    public override IEnumerable<CryptoKey> SubKeys => _subKeys ?? Enumerable.Empty<PublicKeySignature>();


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
    /// <returns></returns>
    internal PublicKeySignature? MatchFingerprint(ReadOnlyMemory<byte> fingerprint, CryptoAlgorithm algorithm = default)
    {
        int len = Math.Min(fingerprint.Length, fingerprint.Length);

        if (fingerprint.Span.Slice(fingerprint.Length - len)
            .SequenceEqual(Fingerprint.Span.Slice(Fingerprint.Length - len)))
        {
            return this;
        }

        return _subKeys?.FirstOrDefault(x => x.MatchesFingerprint(fingerprint, algorithm)) as PublicKeySignature;
    }

    public static bool TryParse(string keyText, [NotNullWhen(true)] out PublicKeySignature? value)
    {
        return TryParse(keyText, null, out value);
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
        using var sig = new SignatureBucket(r) { GetPassPhrase = getPassPhrase };

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

    public IReadOnlyList<BigInteger> GetValues(bool publicOnly=true)
    {
        return Values;
    }

    internal static bool TryParseSshLine(string line, [NotNullWhen(true)] out PublicKeySignature? value)
    {
        if (string.IsNullOrWhiteSpace(line))
            throw new ArgumentNullException(nameof(line));

        line = line.Trim();

        string[] items = line.Split(new char[] { ' ' }, 3);
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

        switch (alg)
        {
            case "ssh-rsa":
                value = new PublicKeySignature(data, CryptoAlgorithm.Rsa,
                    new[]
                    {
                        vals[2],
                        vals[1],
                    }
                );
                return true;
            case "ssh-dss":
                value = new PublicKeySignature(data, CryptoAlgorithm.Dsa,
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
                value = new PublicKeySignature(data, CryptoAlgorithm.Ed25519,
                    new[]
                    {
                        vals[1],
                    });
                return true;
            case "ecdsa-sha2-nistp256":
            case "ecdsa-sha2-nistp384":
            case "ecdsa-sha2-nistp521":
                {
                    var SignaturePublicKey = CryptoDataBucket.GetEcdsaValues(vals.Skip(1).ToArray());
                    value = new PublicKeySignature(data, CryptoAlgorithm.Ecdsa, SignaturePublicKey);
                    return true;
                }
            default:
                throw new NotImplementedException($"SSH public key format {items[0]} not implemented yet");
        }
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
            for (int i = 0; i < 16; i++)
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
