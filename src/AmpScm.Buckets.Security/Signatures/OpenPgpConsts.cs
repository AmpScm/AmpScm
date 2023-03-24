using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Signatures
{
    internal enum OpenPgpTagType
    {
        /// <summary>Reserved</summary>
        None = 0,
        PublicKeySession = 1,
        Signature = 2,
        SymetricSessionKey = 3,
        OnePassSignature = 4,
        SecretKey = 5,
        PublicKey = 6,
        SecretSubkey = 7,
        CompressedData = 8,
        SymetricEncryptedData = 9,
        Marker = 10,
        Literal = 11,
        Trust = 12,
        UserID = 13,
        PublicSubkey = 14,
        UserAttributePacket = 17,
        SymetricEncryptedIntegrity = 18,
        ModificationDetected = 19,
        OCBEncryptedData = 20,

        // 21-59 undefined yet
        //60 to 63 -- Private or Experimental Values


        DerValue = 10021,
    }

    internal enum OpenPgpSignatureType : byte
    {
        BinaryDocument = 0x00,
        CanonicalTextDocument = 0x01, // EOL -> CRLF
        Standalone = 0x02,
        GenericCertification = 0x10,
        PersonaCertification = 0x11,
        CasualCertification = 0x12,
        PositiveCertification = 0x13,
    }

    internal enum OpenPgpSubPacketType : byte
    {
        /// <summary>Reserved</summary>
        None = 0,
        SignatureCreationTime = 2,
        SignatureExpirationTime = 3,
        ExportableCertification = 4,
        TrustSignature = 5,
        RegularExpression = 6,
        Revocable = 7,
        KeyExpirationTime = 9,
        PreferredSymetricAlgorithms = 11,
        RevocationKey = 12,
        Issuer = 16,
        NotationData = 20,
        PreferredHashAlgorithms = 21,
        PreferredCompressionAlgorithms = 22,
        KeyServerPreferences = 23,
        PreferredKeyServer = 24,
        PrimaryUserID = 25,
        PolicyUri = 26,
        KeyFlags = 27,
        SignersUserID = 28,
        ReasonForRevocation = 29,
        Features = 30,
        SignatureTarget = 31,
        EmbeddedSignature = 32,

        // GPG specific?
        IssuerFingerprint = 33,
        PreferredAeadAlgorithms = 34,
        AttestedCertifications = 37,
        EntireKeyUsed = 38,

        FlagCritical = 128
    }

    internal enum OpenPgpHashAlgorithm : byte
    {
        None,
        MD5 = 1,
        SHA1 = 2,
        MD160 = 3,
        SHA256 = 8,
        SHA384 = 9,
        SHA512 = 10,
        SHA224 = 11,
        SHA256v3 = 12,
        // 13 is reserved
        SHA512v3 = 14,
    }

    internal enum OpenPgpPublicKeyType
    {
        None,
        Rsa = 1,
        RsaEncryptOnly = 2,
        RsaSignOnly = 3,
        Elgamal = 16,
        Dsa = 17,
        ECDH = 18,
        ECDSA = 19,
        DHE = 21,
        EdDSA = 22,
        AEDH = 23,
        AEDSA = 24,

        // Outside PGP range, used for ssh and openpgp specialized handling
        Ed25519 = 0x1001,
        Curve25519 = 0x1002,
    }

    internal enum OpenPgpSymmetricAlgorithm
    {
        None,
        Idea = 1,
        T3Des = 2,
        Cast5 = 3,
        Blowfish128 = 4,
        Aes = 7,
        Aes192 = 8,
        Aes256 = 9,
        TwoFish = 10,
        Camellia128 = 11,
        Camellia192 = 12,
        Camellia256 = 13
    }

    internal enum OpenPgpCompressionType
    {
        None,
        Zip = 1,
        Zlib = 2,
        Bzip2 = 3,
    }

    internal enum OpenPgpAeadAlgorithm
    {
        None,
        Eax = 1,
        Ocb = 2
    }
}
