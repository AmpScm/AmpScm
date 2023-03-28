using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Cryptography;


public enum CryptoAlgorithm
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


public record class CryptoKey
{
    private protected CryptoKey(CryptoAlgorithm algorithm)
    {
        Algorithm = algorithm;
    }

    public CryptoAlgorithm Algorithm { get; }

    [return: NotNullIfNotNull(nameof(key1)), NotNullIfNotNull(nameof(key2))]
    public static CryptoKeyChain? operator +(CryptoKey? key1, CryptoKey? key2)
    {
        if (key1 is null)
            return key2;
        else if (key2 is null)
            return key1;

        return new CryptoKeyChain(key1, key2);
    }

    [return: NotNullIfNotNull(nameof(key))]
    public static implicit operator CryptoKeyChain?(CryptoKey? key)
    {
        return key is null ? null : new CryptoKeyChain(key);
    }

    public CryptoKeyChain ToCryptoKeyChain()
    {
        return new CryptoKeyChain(this);
    }

    public virtual IEnumerable<CryptoKey> SubKeys => Enumerable.Empty<CryptoKey>();

    public virtual bool MatchesFingerprint(ReadOnlyMemory<byte> fingerprint, CryptoAlgorithm algorithm = default, bool requirePrivateKey = false)
    {
        return false;
    }
}

public record class AsymetricKey : CryptoKey
{
    protected AsymetricKey(CryptoKey original) : base(original)
    {
    }

    private protected AsymetricKey(CryptoAlgorithm algorithm) : base(algorithm)
    {
    }

    public virtual bool HasPrivateKey { get; protected init; }
}

public sealed class CryptoKeyChain : IEnumerable<CryptoKey>
{
    IEnumerable<CryptoKey> Items { get; init; }

    public CryptoKeyChain(CryptoKey key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        Items = new[] { key };
    }

    public CryptoKeyChain(params CryptoKey[] keys)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));

        Items = keys;
    }

    public CryptoKeyChain(IEnumerable<CryptoKey> keys)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));

        Items = keys;
    }


    [return: NotNullIfNotNull(nameof(key1)), NotNullIfNotNull(nameof(key2))]
    public static CryptoKeyChain? operator +(CryptoKeyChain? key1, CryptoKeyChain? key2)
    {
        if (key1 is null)
            return key2;
        else if (key2 is null)
            return key1;

        return new CryptoKeyChain(key1.Items.Concat(key2.Items));
    }

    IEnumerator<CryptoKey> IEnumerable<CryptoKey>.GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)Items).GetEnumerator();
    }
}
