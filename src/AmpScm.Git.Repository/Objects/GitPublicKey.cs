using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using AmpScm.Buckets.Cryptography;

namespace AmpScm.Git.Objects;

public sealed class GitPublicKey
{
    private readonly PublicKeySignature _key;
    private ReadOnlyMemory<byte> _fingerprint;

    public string? Principal { get; init; }

    internal GitPublicKey(PublicKeySignature key)
    {
        _key = key;
    }


    [return: NotNullIfNotNull(nameof(k))]
    [CLSCompliant(false)]
    public static implicit operator PublicKeySignature?(GitPublicKey k) => k?._key;

    [CLSCompliant(false)]
    public PublicKeySignature ToPublicKeySignature()
    {
        return _key;
    }

    public static bool TryParse(string keyText, [NotNullWhen(true)] out GitPublicKey? result)
    {
        return TryParse(keyText, out result, principal: null);
    }

    internal static bool TryParse(string keyText, [NotNullWhen(true)] out GitPublicKey? result, string? principal = null)
    {
        if (PublicKeySignature.TryParse(keyText, out var v))
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

    public string FingerprintString => _key.FingerprintString;

    internal static ReadOnlyMemory<byte> HashPrint(ReadOnlyMemory<byte> fp)
    {
        if (fp.Length == 8) // 8 = PGP fingerprint
            return fp;
        else if (fp.Length <= (32 /* SHA256 */ + 1))
            return fp;

#if NET6_0_OR_GREATER
        return SHA256.HashData(fp.Span);
#else
        using var sha = SHA256.Create();

        return sha.ComputeHash(fp.ToArray());
#endif
    }
}
