using Chaos.NaCl.Internal.Ed25519Ref10;

namespace Chaos.NaCl;

#pragma warning disable CS8604 // Possible null reference argument.
internal static class Ed25519
{
    /// <summary>
    /// Public Keys are 32 byte values. All possible values of this size a valid.
    /// </summary>
    public const int PublicKeySize = 32;
    /// <summary>
    /// Signatures are 64 byte values
    /// </summary>
    public const int SignatureSize = 64;
    /// <summary>
    /// Private key seeds are 32 byte arbitrary values. This is the form that should be generated and stored.
    /// </summary>
    public const int PrivateKeySeedSize = 32;
    /// <summary>
    /// A 64 byte expanded form of private key. This form is used internally to improve performance
    /// </summary>
    public const int ExpandedPrivateKeySize = 32 * 2;

    /// <summary>
    /// Verify Ed25519 SignaturePublicKey
    /// </summary>
    /// <param name="SignaturePublicKey">SignaturePublicKey bytes</param>
    /// <param name="message">Message</param>
    /// <param name="publicKey">Public key</param>
    /// <returns>True if SignaturePublicKey is valid, false if it's not</returns>
    public static bool Verify(byte[] SignaturePublicKey, byte[] message, byte[] publicKey)
    {
        if (SignaturePublicKey is null)
            throw new ArgumentNullException(nameof(SignaturePublicKey));
        else if (message is null)
            throw new ArgumentNullException(nameof(message));
        else if (publicKey is null)
            throw new ArgumentNullException(nameof(publicKey));

        if (SignaturePublicKey.Length != SignatureSize || publicKey.Length != PublicKeySize)
            throw new InvalidOperationException();

        return Ed25519Operations.crypto_sign_verify(SignaturePublicKey, message, publicKey);
    }
}
