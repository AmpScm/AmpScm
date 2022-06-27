using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Signatures
{
    enum OpenPgpTagType
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
        AEADEncryptedData = 20,

        // 21-59 undefined yet
        //60 to 63 -- Private or Experimental Values


        DerValue = 10021,
    }

    enum OpenPgpSignatureType : byte
    {
        BinaryDocument = 0x00,
        CanonicalTextDocument = 0x01, // EOL -> CRLF
    }

    enum OpenPgpSubPacketType : byte
    {
        /// <summary>Reserved</summary>
        None = 0,
        SignatureCreationTime = 2,
        SignatureExpirationTime = 3,
        Issuer = 16,
        IssuerFingerprint = 33,
    }

    enum OpenPgpHashAlgorithm : byte
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

    enum OpenPgpPublicKeyType
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
}
