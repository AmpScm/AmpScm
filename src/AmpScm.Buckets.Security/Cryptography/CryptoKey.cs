using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

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

public abstract record class AsymmetricCryptoKey
{
    private protected AsymmetricCryptoKey(CryptoAlgorithm algorithm)
    {
        Algorithm = algorithm;
    }

    public CryptoAlgorithm Algorithm { get; }

    [return: NotNullIfNotNull(nameof(key1)), NotNullIfNotNull(nameof(key2))]
#pragma warning disable CA2225 // Operator overloads have named alternates
    public static CryptoKeyChain? operator +(AsymmetricCryptoKey? key1, AsymmetricCryptoKey? key2)
#pragma warning restore CA2225 // Operator overloads have named alternates
    {
        if (key1 is null)
            return key2;
        else if (key2 is null)
            return key1;

        return new CryptoKeyChain(key1, key2);
    }

    [return: NotNullIfNotNull(nameof(key))]
    public static implicit operator CryptoKeyChain?(AsymmetricCryptoKey? key)
    {
        return key is null ? null : new CryptoKeyChain(key);
    }

    public CryptoKeyChain ToCryptoKeyChain()
    {
        return new CryptoKeyChain(this);
    }

    public virtual IEnumerable<AsymmetricCryptoKey> SubKeys => Enumerable.Empty<AsymmetricCryptoKey>();

    public virtual bool MatchesFingerprint(ReadOnlyMemory<byte> fingerprint, CryptoAlgorithm algorithm = default, bool requirePrivateKey = false)
    {
        return false;
    }

    public bool HasPrivateKey { get; protected set; }

    public abstract IReadOnlyList<BigInteger> GetValues(bool includePrivate=false);

    public abstract ReadOnlyMemory<byte> Fingerprint { get; }
}

public class CryptoKeyChain : IEnumerable<AsymmetricCryptoKey>
{
    protected IEnumerable<AsymmetricCryptoKey> Items { get; init; }

    public CryptoKeyChain()
    {
        Items = Enumerable.Empty<AsymmetricCryptoKey>();
    }

    public CryptoKeyChain(AsymmetricCryptoKey key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        Items = new[] { key };
    }

    public CryptoKeyChain(params AsymmetricCryptoKey[] keys)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));

        Items = keys;
    }

    public CryptoKeyChain(IEnumerable<AsymmetricCryptoKey> keys)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));

        Items = keys;
    }


    [return: NotNullIfNotNull(nameof(key1)), NotNullIfNotNull(nameof(key2))]
#pragma warning disable CA2225 // Operator overloads have named alternates
    public static CryptoKeyChain? operator +(CryptoKeyChain? key1, CryptoKeyChain? key2)
#pragma warning restore CA2225 // Operator overloads have named alternates
    {
        if (key1 is null)
            return key2;
        else if (key2 is null)
            return key1;

        return new CryptoKeyChain(key1.Items.Concat(key2.Items));
    }

#pragma warning disable CA1033 // Interface methods should be callable by child types
    IEnumerator<AsymmetricCryptoKey> IEnumerable<AsymmetricCryptoKey>.GetEnumerator()
#pragma warning restore CA1033 // Interface methods should be callable by child types
    {
        return Items.GetEnumerator();
    }

#pragma warning disable CA1033 // Interface methods should be callable by child types
    IEnumerator IEnumerable.GetEnumerator()
#pragma warning restore CA1033 // Interface methods should be callable by child types
    {
        return ((IEnumerable)Items).GetEnumerator();
    }

    public virtual AsymmetricCryptoKey? FindKey(ReadOnlyMemory<byte> fingerprint, CryptoAlgorithm cryptoAlgorithm = default, bool requirePrivateKey = false)
    {
        foreach (var key in this.Where(x => x.MatchesFingerprint(fingerprint, cryptoAlgorithm, requirePrivateKey)))
        {
            if (key is PublicKeySignature pks)
            {
                var r = pks.MatchFingerprint(fingerprint, cryptoAlgorithm, requirePrivateKey);

                if (r is not null)
                    return key; // Return parent key, not subkey
            }
            else
                return key;
        }
        return null;
    }
}
