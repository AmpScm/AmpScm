using System.Diagnostics;
using System.Net.Mail;
using System.Numerics;
using System.Text;
using AmpScm.Buckets;
using AmpScm.Buckets.Cryptography;
using AmpScm.Buckets.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#if !DEBUG
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
#endif

namespace SecurityBucketTests;

[TestClass]
public partial class PgpTests
{
    private const string Sig1 =
@"-----BEGIN PGP SignaturePublicKey-----

wsBcBAABCAAQBQJioEepCRBK7hj4Ov3rIwAAGoMIAHCHbTas3gShVMMX2dx2r82B
33bY2C8sQ+jFyrJHid8Kq8CMokk2cgPNfELyUw/Sjce4M/CxWq6didx58OOg6nom
XIPqvRsHqFuNpuC0Ku9vW4fuiITD6i8ADPpNwsU2lVVSqwVdmdBCU5PncXyYk3Bs
0P1rgF80R9NepCqZ0FihrscJMqX72F5xiq+EGAa/fz4QWDpi792B1fOOO3Q412R6
KJWsEX/HpigGCiiQ/BHf/z3hj9URNEKOWd2hRAJsvkYzczhV8/yZmSjlg9kUp/Cw
aWhgukgOUppFsmnAfSp4zz0MmV2vbAKJQrrTmi1PmDFXt/mDv5xCifZpWbS46cY=
=7fmL
-----END PGP SignaturePublicKey-----";
    private const string Sig2 = // Similar as sig1 but whitespace at start of line
@"-----BEGIN PGP SignaturePublicKey-----

 wsBcBAABCAAQBQJimgGPCRBK7hj4Ov3rIwAAP10IAGkgEDtRaPWlyreQincqo8KM
 vO5uh/G1JzqO1fxtwfzjJB/u48/c/brHqimIEug76zA6vEkkE9Cl42qNY+0vDeII
 MQFED+td5vxJ1lHchkZDcQg+fASmAi0XfD2FfEXbQgwH80dIITcUTGlCySr76M9o
 pqpK8n1PpJXtWnCpj13J/3G5Ugo//H0YUqZJFedz36RxuKw1W7WwZgCtCUd6xNz+
 cZs0jUC2gwbMTD5sfBOGUVMKTANFKy+4gda3ouCPyAP+ptFIT10LbWptsoLnYgx8
 oJzt9PpjzQpPRp9baotmzN72sIHjh5bMqJ9HpUK/RR6FLUSO0qwi54xxL8RckZ4=
 =37hr
 -----END PGP SignaturePublicKey-----";
    private const string SigDSA =
@"-----BEGIN PGP SignaturePublicKey-----

iIIEABEIACoWIQTHNE8vTjJt+Sudk9934a70jlWGXwUCYrAsPwwcZHNhQGxwdDEu
bmwACgkQd+Gu9I5Vhl/hfgD/XmXduRrXvp8wD7cuKWkKfotF+IIgtCnC7FMf9Eq1
WukA/jvr/XbHcqQmFzmWYxf+k3Q5eqKGtMka41jfCWCPxt0Y
=ofhh
-----END PGP SignaturePublicKey-----";

    // https://superuser.com/questions/308126/is-it-possible-to-sign-a-file-using-an-ssh-key
    private const string SshSig =
@"-----BEGIN SSH SignaturePublicKey-----
U1NIU0lHAAAAAQAAAZcAAAAHc3NoLXJzYQAAAAMBAAEAAAGBAP5ogvLJzK+1q2cwJ7K5y6
tIKRc9y4wv9uAk4cULCN4UDwIBcGU4QiAxuWPsgjxKko/qaQMQWBdBcAUXqLTRQC7z80ER
xbXF0uHT/XZfbCVtkg9pwxsYj8ltpgPmTOfIOGx3IyYx+luJkg5c+iahVupxeHzBGoQ2q7
U5MvnMhAEkF+UR5lUbhfqhEOq6SWGsBydr3I/OCrAYcBBO1elXif//ptXO6z2ZOyQI0WL5
wq+aqJ8VUDCIbfbxWQYisqP8SQHgwHfuYhkpifo537S+/PAf7UHmJbrpOOj8DZ/Hrmymqi
U8uEVHZuZFLE/gvGyDhH5MGYwMywM+B4l8Poi7R5+zWzauov1UZtULQWDDh2z2oJ+/ass7
Zr739TmiilduBQGVENeKJf/N6vcAW7KJ5YjeN6+sVtFCqGWbTxpYz8I/fCuliKR6ZfJ0e7
G/lhTAlFoKZ504izPm1V+18PcT5wQ4igtROShnC31iDLCj6lxxtONEWi45wJPUhvSD7rOA
TQAAAARmaWxlAAAAAAAAAAZzaGE1MTIAAAGUAAAADHJzYS1zaGEyLTUxMgAAAYDf95HBLz
yqmKENS4mr+B2/ycK/nPcVMAN8ieqhNheHJjYWROuMiFOMT+h4CwnWZpsIKgce7KTTmAwz
NrO9UpTpRlueSTUkw/L4g6FhEXONZC5WScLZ2XuKTkqu6vOYsth4AMTvcRtdoEWf7QLbvd
YVuBj+QWZV7GKARdMbM3yp10ISWgDk4Ob3hjAcmAT7+eBrQHE08uIqBX82zVe1dfZKSXoI
1VLzGUI/94N++2jDEX35qxKV9wS/13ZtYSW+k7LCSCzgzTtNXFkmb1eYVq5P4/IXJfGB7Z
Cm/QEhrgEyMptGcY/81fCfXE/ylJSDl9sBzCtSin1E9nFkuA1HGkM9zzPvcAY49k0q5j+O
zMVsThr0xjYrEpCy7Mk+v6B94DsJFvSpycppXmfnYX+H2Umi1qw9hp7d/wb2txmqFStM8g
9JDwomS99jM88rMEhZWi6dRjXlEG4q/OoTKnTmT30Aib71Ill+sFxEtmGesS8eeJ+js6B7
GtAh3JPRDOlZUZM=
-----END SSH SignaturePublicKey-----";

    [TestMethod]
    [DataRow(Sig1, DisplayName = nameof(Sig1))]
    [DataRow(Sig2, DisplayName = nameof(Sig2))]
    [DataRow(SigDSA, DisplayName = nameof(SigDSA))]
    [DataRow(SshSig, DisplayName = nameof(SshSig))]
    public async Task ParseSignature(string SignaturePublicKey)
    {
        var b = Bucket.Create.FromASCII(SignaturePublicKey);

        await using var sr = new Radix64ArmorBucket(b);

        while (true)
        {
            var bb = await sr.ReadHeaderAsync();
            if (bb.IsEof)
                break;
        }

        var dt = await sr.ReadAtLeastAsync(Bucket.MaxRead, throwOnEndOfStream: false);
    }

    [TestMethod]
    [DataRow(Sig1, DisplayName = nameof(Sig1))]
    [DataRow(Sig2, DisplayName = nameof(Sig2))]
    [DataRow(SigDSA, DisplayName = nameof(SigDSA))]
    [DataRow(SshSig, DisplayName = nameof(SshSig))]
    public async Task ParseSigTail(string SignaturePublicKey)
    {
        var b = Bucket.Create.FromASCII(SignaturePublicKey + Environment.NewLine + "TAIL!");

        await using var sr = new Radix64ArmorBucket(b);

        while (true)
        {
            var bb = await sr.ReadHeaderAsync();
            if (bb.IsEof)
                break;
        }

        var dt = await sr.ReadAtLeastAsync(Bucket.MaxRead, throwOnEndOfStream: false);

        var bt = await b.ReadAtLeastAsync(1024, throwOnEndOfStream: false);
        Assert.AreEqual("TAIL!", bt.ToASCIIString());
    }

    [TestMethod]
    [DataRow(Sig1, DisplayName = nameof(Sig1))]
    [DataRow(Sig2, DisplayName = nameof(Sig2))]
    [DataRow(SigDSA, DisplayName = nameof(SigDSA))]
    [DataRow(SshSig, DisplayName = nameof(SshSig))]
    public async Task ParseRfc4880(string SignaturePublicKey)
    {
        var b = Bucket.Create.FromASCII(SignaturePublicKey + Environment.NewLine + "TAIL!");

        var sr = new Radix64ArmorBucket(b);
        await using var rr = new SignatureBucket(sr);

        var bb = await rr.ReadAtLeastAsync(8192, throwOnEndOfStream: false);

        var bt = await b.ReadAtLeastAsync(1024, throwOnEndOfStream: false);
        Assert.AreEqual("TAIL!", bt.ToASCIIString());
    }

    private const string rsa_key1_fingerprint = "1E4EA61FCC73D2D96075A9835B15B06B7943D080";
    private const string rsa_key1 =
@"-----BEGIN PGP PRIVATE KEY BLOCK-----

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
9GPs2YhVJY76FLNUH7BPGXZ+6jo3KrQA
=8qhj
-----END PGP PRIVATE KEY BLOCK-----";
    private const string rsa_key2_fingerprint = "8B34F2DCDBA7FECF88029C53C5D6650A9716C11A";
    private const string rsa_key2 =
@"-----BEGIN PGP PRIVATE KEY BLOCK-----

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
=uXxW
-----END PGP PRIVATE KEY BLOCK-----
";
    private string msg_from_rsa1_to_rsa2 =
@"-----BEGIN PGP MESSAGE-----

hQGMAzI//wS08Dw8AQv/fy/zx77K2jHV6VVMKIWfQn0YPcUBR8Xv9Mw7ERLeP3wE
6ccxFi7FyVY1/YvqKZIkcVbXmS2t2uqLV8vAM0V7yUEh7EjEPsi2PWibZuDr3v63
5O5X1J3nBPllBTpdIQIJz+Aevp6aA4oXjPv3K3jhIqCAz+4r2mNio9HivuRqngAd
twrFghyoI6aMC0C2w8fsT3gnkEwHEx+GJRIUAgYVkM9idBcrxKxHbMWHhwcBxSmr
s6sAEmkDqi05QsqcG1fxfqDMk9M5Rov8q7v2HVk2OzaL1ZZfSMykg8hEJ4x799Fu
IzMejj9KtvMO79XFSIe3zQ+yOL/K1O4iL9BmmL0EAoW9Fy/AYZwNPijVX1cvAhFF
HQn8QNd2i0mg64Y9pjwIC2961tGUdRS083n1DUPqjBVXJx/TFmZbPrTVtWTfP/3R
5czWWpgE7GlKX1wWhABuw1aN58pohRYFJUXylCbdKeBcJjtTQ3FW4tTA56uVIKoD
pVHrpUyqd5niF6wyo+cz1GABCQIQXWzFLP2s0TCXopzN8EVb/ibP98iPd/KZOaA2
ZYibxRN9ojqM/htBj4dHuO7WQc0HlBzz3zHef7yjLIU1Lx78EzODK23spSD6vujh
p1Sxxzh2DyLOpTYUFshpanw=
=DtZ3
-----END PGP MESSAGE-----
";
    private string msg_from_rsa1_to_rsa2_2 =
@"-----BEGIN PGP MESSAGE-----

hQGMAzI//wS08Dw8AQv/UMXN2e3ew/CBMxzOZaX5pH8OBFVdeNJHHmfZ6b8igdJI
uST4VhJIGf/i4VZAJXcAP6tnqXU0IlSlT+8/BeAVL5FhSJlRVm9BauLMrdw07sa6
M5L7l7hXMZ/wrY+UdAs2LNfGb7Lguxh8hCAZc3mxjqB8vGggEkytkbjkgnO/npbg
CZPhlD5y9letF6Zt8WpOMsygc1T/XPm8N7UmTtzVy7KE3g681p3wXTGzfUPm/bAf
UlkZ7pklDj+/Yt9H3l/yejN11B3jRqwCO3KR/BVQzHtaSDff9u5/bMTfvnifchBz
a6Q+f53tMm6/+RzidZOET9LmXiR5oq352iEUopf+IGqAND8vDKyNIMQNIz0R6Ri7
lD2aQAlYXJTlVxqCVURszIdz/3ak96oU6yZLKaWrtjrgajtUVFHI73upNuM9+WPy
dzb7f7U7VwAkELKq07Lil2snj+JTKZ7UHn/vEmP04WEXFeeajtwC8goHWe+B9Ynk
MkUqCPQPUi6cGBMg/v7M1F8BCQIQXdyioSLI1ebiP50e1LbWIkTIkEcQshz73EUx
/C4bVELiO7vC7eQ4gwVTj0R2WhPlaUkgGBrSPCzQqMYoUw8nw6g27Oq521YxvuzS
FfT7+cRyXwTCQKtCfPAcLA==
=eoW8
-----END PGP MESSAGE-----
";
    private string msg_from_rsa1_to_rsa2_3 =
@"-----BEGIN PGP MESSAGE-----

hQGMAzI//wS08Dw8AQwAlbn1Jx+3QmpCfounrMIPw34bYBxRwNgFUY9oxXMyjH41
Qt97FcE0KpdFBHvBK2w/wAysD5aThNW30a9jFD51Ag3pBMfVqQVD0ONutSdCVNx/
8CRqUNWRaXz5DAZyi3XRTAPy3vzGdn6AS3eJGo4QFguv1cUYI/3DJwO20aLwTqnO
LCYRbMem60tOGmSDvv43I7/+X/aGt27wXzpC+MkMnrlwbmlYHHetIoL5A2UgZNIn
nG+NZavGgiTPlOllf01J5inDQ8POHzlgQcC4lWorika6jVa8uuF024M6BG9lZkyt
eihc4LoRUWZdoLLSH13eyLmJkjNVzy3lXN95ydPak9+2QFCXZCAP65/NcEWRiMHA
nrbeB9hdGclskcxj3+FU7J2RYmKyypi+EIXncBdOmbgkIAn4yZdyi4sD/sueA2LF
5S51gat1Jn3NgOnTkuuh5wht12Fv3PVSAU1v4k4wuHR+BXbwuXOAr/sw7ByH7OcB
Bl4TNszMDuoAyFj/vLnX1F8BCQIQA4mLULz5uEo0KbcKI/AsE72M/Y3bGdFR1j+g
EaQhyNyT/brYiDDe6jgv54KdpWqkNd+UouYsMb25a/6REc4JDNGJ/OPfq/q4LCfB
cEgAjelaGkn3RJOwXWoJbA==
=6KA8
-----END PGP MESSAGE-----
";


    [TestMethod]
    public async Task TestPrivateKeys()
    {
        Assert.IsTrue(PublicKeySignature.TryParse(rsa_key1, out var key1));
        Assert.AreEqual(rsa_key1_fingerprint, key1.FingerprintString);
        Assert.AreEqual((BigInteger)65537, key1.GetValues()[1]);
        Assert.AreEqual(new MailAddress("test@rsa", "RSA Test"), key1.MailAddress);
        Assert.AreEqual(CryptoAlgorithm.Rsa, key1.Algorithm);
        Assert.IsTrue(key1.HasPrivateKey, "Key1 has secret");

        Assert.IsTrue(PublicKeySignature.TryParse(rsa_key2, out var key2));
        Assert.AreEqual(rsa_key2_fingerprint, key2.FingerprintString);
        Assert.AreEqual(new MailAddress("test-3072@rsa", "RSA Test 3072"), key2.MailAddress);
        Assert.AreEqual(CryptoAlgorithm.Rsa, key2.Algorithm);
        Assert.IsTrue(key1.HasPrivateKey, "Key2 has secret");

        await using var decr = new Radix64ArmorBucket(Encoding.ASCII.GetBytes(msg_from_rsa1_to_rsa2).AsBucket());

        await using var dec = new DecryptBucket(decr) { KeyChain = key2 };


        var bb = await dec.ReadAtLeastAsync(1024, throwOnEndOfStream: false);

        Assert.AreEqual("This is the plaintext.\n", bb.ToUTF8String());
    }

    [TestMethod]
    public async Task TestPrivateKeys2()
    {
        Assert.IsTrue(PublicKeySignature.TryParse(rsa_key1, out var key1));
        Assert.AreEqual(rsa_key1_fingerprint, key1.FingerprintString);
        Assert.AreEqual(new MailAddress("test@rsa", "RSA Test"), key1.MailAddress);
        Assert.AreEqual(CryptoAlgorithm.Rsa, key1.Algorithm);
        Assert.IsTrue(key1.HasPrivateKey, "Key1 has secret");

        Assert.IsTrue(PublicKeySignature.TryParse(rsa_key2, out var key2));
        Assert.AreEqual(rsa_key2_fingerprint, key2.FingerprintString);
        Assert.AreEqual(new MailAddress("test-3072@rsa", "RSA Test 3072"), key2.MailAddress);
        Assert.AreEqual(CryptoAlgorithm.Rsa, key2.Algorithm);
        Assert.IsTrue(key1.HasPrivateKey, "Key2 has secret");

        await using var decr = new Radix64ArmorBucket(Encoding.ASCII.GetBytes(msg_from_rsa1_to_rsa2_2).AsBucket());

        await using var dec = new DecryptBucket(decr) { KeyChain = key1 + key2 };


        var bb = await dec.ReadAtLeastAsync(1024, throwOnEndOfStream: false);

        Assert.AreEqual("This is the plaintext.", bb.ToUTF8String());
    }

    [TestMethod]
    public async Task TestPrivateKeys3()
    {
        Assert.IsTrue(PublicKeySignature.TryParse(rsa_key1, out var key1));
        Assert.AreEqual(rsa_key1_fingerprint, key1.FingerprintString);
        Assert.AreEqual(new MailAddress("test@rsa", "RSA Test"), key1.MailAddress);
        Assert.AreEqual(CryptoAlgorithm.Rsa, key1.Algorithm);
        Assert.IsTrue(key1.HasPrivateKey, "Key1 has secret");

        Assert.IsTrue(PublicKeySignature.TryParse(rsa_key2, out var key2));
        Assert.AreEqual(rsa_key2_fingerprint, key2.FingerprintString);
        Assert.AreEqual(new MailAddress("test-3072@rsa", "RSA Test 3072"), key2.MailAddress);
        Assert.AreEqual(CryptoAlgorithm.Rsa, key2.Algorithm);
        Assert.IsTrue(key1.HasPrivateKey, "Key2 has secret");

        await using var decr = new Radix64ArmorBucket(Encoding.ASCII.GetBytes(msg_from_rsa1_to_rsa2_3).AsBucket());

        await using var dec = new DecryptBucket(decr) { KeyChain = key1 + key2 };


        var bb = await dec.ReadAtLeastAsync(1024, throwOnEndOfStream: false);

        Assert.AreEqual("This is the plaintext.", bb.ToUTF8String());
    }

    [TestMethod]
    public async Task TestOCBTestData()
    {
        ReadOnlyMemory<byte> r = new byte[]
        {
            // Header
            0xc3, 0x3d,
            // Version, alg, etc.
            0x05, 0x07, 0x02, 0x03, 0x08, 0x9f, 0x0b, 0x7d, 0xa3, 0xe5, 0xea, 0x64, 0x77, 0x90,
            // OCB IV
            0x99, 0xe3, 0x26, 0xe5, 0x40, 0x0a, 0x90, 0x93, 0x6c, 0xef, 0xb4, 0xe8, 0xeb, 0xa0, 0x8c,
            // OCB encrypted CEK
            0x67, 0x73, 0x71, 0x6d, 0x1f, 0x27, 0x14, 0x54, 0x0a, 0x38, 0xfc, 0xac, 0x52, 0x99, 0x49, 0xda,
            //  Authentication tag:
            0xc5, 0x29, 0xd3, 0xde, 0x31, 0xe1, 0x5b, 0x4a, 0xeb, 0x72, 0x9e, 0x33, 0x00, 0x33, 0xdb, 0xed,

            // Header
            0xd4, 0x49,
            // Version, AES-128, OCB, Chunk bits (14):
            0x01, 0x07, 0x02, 0x0e, 
            // IV
            0x5e, 0xd2, 0xbc, 0x1e, 0x47, 0x0a, 0xbe, 0x8f, 0x1d, 0x64, 0x4c, 0x7a, 0x6c, 0x8a, 0x56,
            //  OCB Encrypted data chunk #0:
            0x7b, 0x0f, 0x77, 0x01, 0x19, 0x66, 0x11, 0xa1, 0x54, 0xba, 0x9c, 0x25, 0x74, 0xcd, 0x05, 0x62,
            0x84, 0xa8, 0xef, 0x68, 0x03, 0x5c,
            // Chunk #0 authentication tag:
            0x62, 0x3d, 0x93, 0xcc, 0x70, 0x8a, 0x43, 0x21, 0x1b, 0xb6, 0xea, 0xf2, 0xb2, 0x7f, 0x7c, 0x18,
            //Final (zero-size chunk #1) authentication tag:
            0xd5, 0x71, 0xbc, 0xd8, 0x3b, 0x20, 0xad, 0xd3, 0xa0, 0x8b, 0x73, 0xaf, 0x15, 0xb9, 0xa0, 0x98
        };

        var b = new DecryptBucket(r.AsBucket()) { GetPassword = (_) => "password" };

        string result = "";
        while (true)
        {
            var bb = await b.ReadAsync(1024);

            var part = bb.ToASCIIString();
            result += part;
            Trace.Write(part);

            if (bb.IsEof)
                break;
        }

        Assert.AreEqual("Hello, world!\n", result);
    }

    [TestMethod]
    public async Task TestOCBSplit()
    {
        byte[] key = new byte[] { 0xeb, 0x9d, 0xa7, 0x8a, 0x9d, 0x5d, 0xf8, 0x0e, 0xc7, 0x02, 0x05, 0x96, 0x39, 0x9b, 0x65, 0x08 };
        byte[] nonce = new byte[] { 0x99, 0xe3, 0x26, 0xe5, 0x40, 0x0a, 0x90, 0x93, 0x6c, 0xef, 0xb4, 0xe8, 0xeb, 0xa0, 0x8c };
        byte[] authenticated = new byte[] { 0xc3, 0x05, 0x07, 0x02 };


        ReadOnlyMemory<byte> crypted = new byte[]
        {
            // OCB encrypted CEK
            0x67, 0x73, 0x71, 0x6d, 0x1f, 0x27, 0x14, 0x54, 0x0a, 0x38, 0xfc, 0xac, 0x52, 0x99, 0x49, 0xda,
            //  Authentication tag:
            0xc5, 0x29, 0xd3, 0xde, 0x31, 0xe1, 0x5b, 0x4a, 0xeb, 0x72, 0x9e, 0x33, 0x00, 0x33, 0xdb, 0xed,
        };

        bool ok = false;
        var d = new OcbDecodeBucket(crypted.AsBucket(), key, 128,
            nonce: nonce, associatedData: authenticated,
            verifyResult: x => ok = x);

        var r = await d.ReadAtLeastAsync(1024, throwOnEndOfStream: false);

        Assert.AreEqual(16, r.Length);
        Assert.IsTrue(new byte[] { 0xd1, 0xf0, 0x1b, 0xa3, 0x0e, 0x13, 0x0a, 0xa7, 0xd2, 0x58, 0x2c, 0x16, 0xe0, 0x50, 0xae, 0x44 }
            .SequenceEqual(r.ToArray()), "Result matches");

        Assert.IsTrue(ok, "Verified as ok");
    }

    [TestMethod]
    public async Task TestOCB()
    {
        var key = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F }.ToArray();

        var crypted = new byte[]
        {
            0x44, 0x12, 0x92, 0x34, 0x93, 0xC5, 0x7D, 0x5D,  0xE0, 0xD7, 0x00, 0xF7, 0x53, 0xCC, 0xE0, 0xD1,
            0xD2, 0xD9, 0x50, 0x60, 0x12, 0x2E, 0x9F, 0x15,  0xA5, 0xDD, 0xBF, 0xC5, 0x78, 0x7E, 0x50, 0xB5,
            0xCC, 0x55, 0xEE, 0x50, 0x7B, 0xCB, 0x08, 0x4E,  0x47, 0x9A, 0xD3, 0x63, 0xAC, 0x36, 0x6B, 0x95,
            0xA9, 0x8C, 0xA5, 0xF3, 0x00, 0x0B, 0x14, 0x79
        };

        bool ok = false;
        var d = new OcbDecodeBucket(crypted.AsBucket(), key, 128,
            nonce: new byte[] { 0xBB, 0xAA, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x0F },
            verifyResult: x => ok = x);

        var r = await d.ReadAtLeastAsync(1024, throwOnEndOfStream: false);

        Assert.AreEqual(40, r.Length);
        Assert.IsTrue(new byte[] {
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,  0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,  0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
            0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, }.SequenceEqual(r.ToArray()));

        Assert.IsTrue(ok);
    }

    private const string APrivateKey =
@"-----BEGIN PGP PRIVATE KEY BLOCK-----

lFgEY+jHGBYJKwYBBAHaRw8BAQdA4LuQh88dckAm8tn5rJS5fIzkbLKtoSRvTlA9
gRUdsTsAAQCS0d7Q7Aly4mzeuebeddphO8s6i8XsCWDuRLFPFwkbaRGNtAtBQUFB
QSA8QUBBPoiZBBMWCgBBFiEEBTvJdaqKWVTRQK6y4WOf/s9/93QFAmPoxxgCGwMF
CQPCZwAFCwkIBwICIgIGFQoJCAsCBBYCAwECHgcCF4AACgkQ4WOf/s9/93QKRAD9
EqOwr3BNO/9+ZcVjovdGIY2bOqkNaaCzXwK/BXHEJqUBAILvjKAAHhKIqwsO9Xj0
cYsZCutyrm7HG5BkK4bx2pgCnF0EY+jHGBIKKwYBBAGXVQEFAQEHQD3du+VhSTvQ
fSzYKncSaG64ywISypob2HcsnxWIcj0LAwEIBwAA/3SmrDagn6JMteDoQBEPO3kA
nWzmaWWTGa7x71JrNOk4EMKIfgQYFgoAJhYhBAU7yXWqillU0UCusuFjn/7Pf/d0
BQJj6McYAhsMBQkDwmcAAAoJEOFjn/7Pf/d0jpgA/0vyiJ9Ov3NJIJo3NZjDU/cO
TLQz69WEUUAtQ1SwiGAaAP9MaVfEDY7GtSZO97Vs5prfBkYhWc3qEknTOhfiZbql
BA==
=PIZ4
-----END PGP PRIVATE KEY BLOCK-----";
    private const string AMessage =
@"-----BEGIN PGP MESSAGE-----

hF4DxaX2D8f75n0SAQdACeR83BHHiw1jGFVw4TcPcoHqOyZdl/9AHvY8TlkIf38w
j7ym7GaQCbYP17fNlYSzYPsKJBF6EICRIs5p/GkleM5nCGAHlOAYaWnH594fkCC1
1FoBCQIQ+1JDVMpNBbPXGDQrt3zEC3I7FGj2+tDrp0Bq2+kKGLfdHXxtfDsflXGq
sVx2nlctyiV9c8zOnUfmZkqI1QjzinfHbpuNi80ah4eIGQ/YY+lo5Bpnbfs=
=Y8Z7
-----END PGP MESSAGE-----";

    [TestMethod]
    public async Task ParsePrivateKey()
    {
        var b = Bucket.Create.FromASCII(APrivateKey);

        var r = new Radix64ArmorBucket(b);
        await using var sig = new SignatureBucket(r);

        var key = await sig.ReadKeyAsync();

        Assert.AreEqual("053BC975AA8A5954D140AEB2E1639FFECF7FF774", key.FingerprintString);
        Assert.AreEqual(CryptoAlgorithm.Ed25519, key.Algorithm);

        Assert.IsTrue(PublicKeySignature.TryParse(APrivateKey, out var value));
        Assert.IsTrue(value.HasPrivateKey);

        Assert.AreEqual("053BC975AA8A5954D140AEB2E1639FFECF7FF774", value.FingerprintString);
    }

    [TestMethod]
    public void TestS2k()
    {
        const string Privy = @"-----BEGIN PGP PRIVATE KEY BLOCK-----

lQIGBGQdww4BBACkI8oezv1FJd1ykfGbeu1Idmrs5Zdv7ano+HYumEAQ/eUfAogG
XtDjHA5sCfWYM9KmJwtMr43G80uHcqZOyg12e8qWGRM9WbSqALETVhUSX9+p5Qw2
IdDIhDQznxNJjDzdgaFChkPdF0LUwLfiZNxpJvozs0eWel4Wpa8S0XeA0wARAQAB
/gcDAq2UHlIGny13ys1urk56iABGMj03Ou4E3NzfMH2khe28sxRCvELdCFRlh73I
yLGk3yZX4tCHylTpzhOTQ2HoLb2CVAXJDb6l+OfKZ4ezS2EfnjtWDqtG5CkJeVnk
o97QobOxAGj+z3VrCZDYDRxFIoeZuW/+0ijYNcBjoSuXljiG9+jmGh0wM2mbEGhg
1L1sKvX/eB452Y4pBa2wTBjRxcOGSsz3vvbZ/GlOO3PP0Oek3T/Kk/dTOPBNzOtQ
ZN8c8exNmA+yOXghnFX8hBniJTSCAotYQ3VNGk3osaKGw+UyfAqKYsvow8sziXuS
seKNOKxEC4bHyjjm7c4w6NJt8cK8T0nd1zzMHOdbTJEH3qTR8dpNWplxRz27fG0g
wFv9UqlosqvytgvdhzNQHtuh/EgEaft5BLvAPvEZeIhDR/bxnEEhVqzLEPIjAuFe
hX/hgEPEuwc3PuLG2YwbiHmC8WV5bsbdF8Y4V96DujWD5f+Idycxas+0Fk1lIE15
IChIZWxsbyEpIDxtZUBteT6I0QQTAQIAOxYhBCptbLkFCNC5L6XgAEH2EPUacRkB
BQJkHcMOAhsDBQsJCAcCAiICBhUKCQgLAgQWAgMBAh4HAheAAAoJEEH2EPUacRkB
NxsD/R2MLYUyX18kVDbnQy4hhtILz/6EarBoBk6lsLL03yXwPtPqFzV48Yvh/Pgz
VpuPq889LPyZD2YNYfSCcurBR3njXxjNrhDv3VJ+EI3iSTp5/DmqjtIy/1yGtzai
JeKFJXXDjLPCLX8Dio6NFL/8pZnaUwOFCV6bQiI92kgVDbM2nQIGBGQdww4BBADb
Xtah1QFxcZg3DmBsSV/UknmmFtILm5ws/XjfXZbDasmyE+TNgTViRdvM5bLQY90f
Yi3TTYajvB1HEWatACugmxXRHYA/UTzbTqG5CYkIW+Wq5/S8Qlb+3vyfQAiV5sec
Mz170ztLI2W7PdgV8xejSyjGq3aB5Jx4W8a89jJj6wARAQAB/gcDAv7KUmLi1d0w
ylMuCIEFxm/bckI0sZVgWORnfi/lxv6JJ5l/DFk6pr+eAwkmFXuUIthRN4Knyq0o
VDxKbyHJyGsEZZ+Yt0+UfNrWW3b9J+O18+HnFrWrlNGoFEDZSZJtyzPq1x/vUlcL
wHEidAT1YzWptgGIoPPIZha7eZx7lH1sG6+ur8ZNa7CRgQWV9n0b3q2yIh6jAHX5
rC8yXJrVIJfJmuGczEfeDjgE8wD71eKah7AP0sazffr0xGNHFvt6AEqlsNnPbhjZ
WfGDk/C2/wH1xMHiPwXIAOVPjtM/vDwvHD2cWn+3DC5DHbV/mIgrREoX5aPeQM6/
wZTdaaW2uRhpEa4zeDuiJaBiJsli4wHgRPEIt432dGhPXBeuOYSBo3ixwanIY9M8
Jmg4PDCt39nZxDF7Amk0cve5RKvBf+xkJPjiviyS4OhNaMLzA/N78t9EqsvGtFdb
MaZ3bpaMmMIAvUpUiw8AhIyVWcoNmReZfctP0/aItgQYAQIAIBYhBCptbLkFCNC5
L6XgAEH2EPUacRkBBQJkHcMOAhsMAAoJEEH2EPUacRkB5bYEAInu44ZZVIjWKQZa
He3IjpHSAKQt9yKrmgOZrsP8CoyC6oaS28SHda9G+pu6cQWcXHHnOvhcZzH0Ho8X
4jwA+cKhREH4Oc/PQfydL3ZLC0be0HFNJ2OCTs3yAOnTqOsC056V1ClmaR/W8/Os
f9nwhs2r0FA7IKmrcLiL2sClVAAl
=KGCH
-----END PGP PRIVATE KEY BLOCK-----";


        Assert.IsTrue(PublicKeySignature.TryParse(Privy, _ => "I", out var key));

        Assert.IsTrue(key.HasPrivateKey);
    }

    [TestMethod]
    public async Task AToB()
    {
        Assert.IsTrue(PublicKeySignature.TryParse(rsa_key1, out var key1));
        Assert.IsTrue(PublicKeySignature.TryParse(rsa_key2, out var key2));

        Assert.IsTrue(key1.HasPrivateKey);
        Assert.IsTrue(key2.HasPrivateKey);

        var raw_msg = Bucket.Create.FromASCII(msg_from_rsa1_to_rsa2_3);

        await using var dc = new DecryptBucket(new Radix64ArmorBucket(raw_msg))
        {
            KeyChain = key1 + key2
        };

        var bb = await dc.ReadAtLeastAsync(1024, throwOnEndOfStream: false);

        Assert.AreEqual("This is the plaintext.", bb.ToUTF8String());
    }

    [TestMethod]
    public async Task DecryptJustPW128()
    {
        const string msg =
@"-----BEGIN PGP MESSAGE-----

jA0EBwMCQi+5GH+oZubQ0koB9PWodsCh7cj0Eayi8bNBF5KxVwBt/TgEEosQPAX5
6j4aSmcXRbkynrmAZ6j6ThyhiOobPoI3kywooZKEpAstmWVv7knpt7d/BQ==
=dr8v
-----END PGP MESSAGE-----";

        // Message is AES encrypted, so needs one pass through S2k
        await using var dc = new DecryptBucket(new Radix64ArmorBucket(Bucket.Create.FromASCII(msg)))
        {
            GetPassword = (_) => "PW"
        };

        var bb = await dc.ReadAtLeastAsync(1024, throwOnEndOfStream: false);

        Assert.AreEqual("Password encrypted.\r\n", bb.ToUTF8String());
    }

    [TestMethod]
    public async Task DecryptJustPW256()
    {
        const string msg =
@"-----BEGIN PGP MESSAGE-----

jA0ECQMCCrVgSxB6fx/O0kEBtIi5fgY9ki64HJl0hlet3PsZXkNTfbPapgrjv7CQ
twLUuM6VyHcewSpuu2Lv06liNCE5Kn+TsTmgR/PmD8yLow==
=tq7d
-----END PGP MESSAGE-----";

        // Message is AES256 encrypted, so needs two passes through S2k
        await using var dc = new DecryptBucket(new Radix64ArmorBucket(Bucket.Create.FromASCII(msg)))
        {
            GetPassword = (_) => "PW"
        };

        var bb = await dc.ReadAtLeastAsync(1024, throwOnEndOfStream: false);

        Assert.AreEqual("Some text \r\n", bb.ToUTF8String());
    }



    [TestMethod]
    public async Task DecryptTwice()
    {
        // echo Some more text |gpg --passphrase PW -r 1E4EA61FCC73D2D96075A9835B15B06B7943D080 -e -c -a
        const string msg =
@"-----BEGIN PGP MESSAGE-----

hQEMA/dXtoIexmrPAQf8DELsvEv9QIleXuH0KH2w+WUCdZ45MD5QLO4G2Gtp8SIf
0oabipPX7NK6vsP5GAVjXLXH7SltO7KB37YCR+C6arn7reBJBurkBIRkUiRZaB4F
VcKZ6hxkrNCe8xu78dbIfYP+6iWm6DO4wRM8umqGczXZymbWMXFL+KVGt0wTEMzT
NWOoIKd8rggevsvBTSPyKFHn1rfGb3cfnf3TySRRwQPy2+JDS4wnyGitPDQtfkRe
D1Wj18evNDIL6PsRYijzLg8+RBr4X0opCDGZju9004+IrGopAvg2WwNhCt9DTTxq
YeAL83loRTDRTHs5nsySoPaVUilKY2GIiqdXG8sSkIxNBQkCAwKj0GWFlFWLVM4Q
DtmmHkI7ULe9SKkhtf5sEOuNDwlP8FBstLMLeZ9AFeknUGBghp3Z2U9m5lwHHyzu
3FCTOWwHyqzdFF33qv/UVgEJAhDZnho7gudz6eEQD90PO1ybDRBOQJFRlt3yJUCF
wJmwSs2v4TD1KbyUJk5mUdugJ6X+Pee2eaKgteQwIZW1r/46bjU4WsjkxwvXTdAz
UL6Ey7aK
=WHnI
-----END PGP MESSAGE-----";

        await using (var dc = new DecryptBucket(new Radix64ArmorBucket(Bucket.Create.FromASCII(msg))) { GetPassword = (_) => "PW" })
        {

            // Decrypt using password
            var bb = await dc.ReadAtLeastAsync(1024, throwOnEndOfStream: false);
            Assert.AreEqual("Some more text \r\n", bb.ToUTF8String());

        }

        Assert.IsTrue(PublicKeySignature.TryParse(rsa_key1, out var key1));
        await using (var dc2 = new DecryptBucket(new Radix64ArmorBucket(Bucket.Create.FromASCII(msg)))
        {
            KeyChain = key1
        })
        {

            // Decrypt using key
            var bb = await dc2.ReadAtLeastAsync(1024, throwOnEndOfStream: false);
            Assert.AreEqual("Some more text \r\n", bb.ToUTF8String());
        }
    }

    [TestMethod]
    public async Task DecryptSeek()
    {
        // echo Some more text |gpg --passphrase PW -r 1E4EA61FCC73D2D96075A9835B15B06B7943D080 -e -c -a
        const string msg =
@"-----BEGIN PGP MESSAGE-----

hQEMA/dXtoIexmrPAQf8DELsvEv9QIleXuH0KH2w+WUCdZ45MD5QLO4G2Gtp8SIf
0oabipPX7NK6vsP5GAVjXLXH7SltO7KB37YCR+C6arn7reBJBurkBIRkUiRZaB4F
VcKZ6hxkrNCe8xu78dbIfYP+6iWm6DO4wRM8umqGczXZymbWMXFL+KVGt0wTEMzT
NWOoIKd8rggevsvBTSPyKFHn1rfGb3cfnf3TySRRwQPy2+JDS4wnyGitPDQtfkRe
D1Wj18evNDIL6PsRYijzLg8+RBr4X0opCDGZju9004+IrGopAvg2WwNhCt9DTTxq
YeAL83loRTDRTHs5nsySoPaVUilKY2GIiqdXG8sSkIxNBQkCAwKj0GWFlFWLVM4Q
DtmmHkI7ULe9SKkhtf5sEOuNDwlP8FBstLMLeZ9AFeknUGBghp3Z2U9m5lwHHyzu
3FCTOWwHyqzdFF33qv/UVgEJAhDZnho7gudz6eEQD90PO1ybDRBOQJFRlt3yJUCF
wJmwSs2v4TD1KbyUJk5mUdugJ6X+Pee2eaKgteQwIZW1r/46bjU4WsjkxwvXTdAz
UL6Ey7aK
=WHnI
-----END PGP MESSAGE-----";

        await using (var dc = new DecryptBucket(new Radix64ArmorBucket(Bucket.Create.FromASCII(msg))) { GetPassword = (_) => "PW" })
        {
            var s = dc.NoDispose().AsStream();

            s.Seek(0, SeekOrigin.Begin); // Stupid hardcoded default in some third party code

            using (var sr = new StreamReader(s))
            {
                Assert.AreEqual("Some more text \r\n", sr.ReadToEnd());
            }
        }


        await using (var dc = new DecryptBucket(new Radix64ArmorBucket(Bucket.Create.FromASCII(msg))) { GetPassword = (_) => "PW" })
        {

            var bb = await dc.ReadAtLeastAsync(5, throwOnEndOfStream: false);

            Assert.AreEqual(5, bb.Length);

            await dc.SeekAsync(0);


            bb = await dc.ReadAtLeastAsync(1024, throwOnEndOfStream: false);

            Assert.AreEqual("Some more text \r\n", bb.ToUTF8String());
        }
    }


    [TestMethod]
    public async Task DecryptTripleDes()
    {
        // echo "This is (was) triple des" | gpg --batch --passphrase 3des --allow-old-cipher-algo --cipher-algo 3DES --symmetric -a
        const string msg =
@"-----BEGIN PGP MESSAGE-----

jA0EAgMCn0hY6F1DihXM0kQBUdLXmnQ4eQ8oHo/6FXiQp2Z4vFqtY5L1UaDF4l3p
meYB0A6OXDi4z0cpa097TG9MiC3vmRIpUjpPr8WeDJA2r03imw==
=hDJ4
-----END PGP MESSAGE-----
";

        await using (var dc = new DecryptBucket(new Radix64ArmorBucket(Bucket.Create.FromASCII(msg))) { GetPassword = (_) => "3des" })
        {

            // Decrypt using password
            var bb = await dc.ReadAtLeastAsync(1024, throwOnEndOfStream: false);
            Assert.AreEqual("This is (was) triple des\n", bb.ToUTF8String());

        }
    }

    [TestMethod]
    public void DecodeRSAKey()
    {
        var k = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAxpaMTz2NdmNZMoUBgRC+
/hBNbKoOQWvlN5YKzHRrBl0wd0m8uPmexrqMnhxHxtYXn0zd23/ooW3Jy258tFhz
gMcjC8Gm0Zo722XEtNP1exN6mUck/ARPrndmvyoQOqfOOiqwerCAWv35d9dtKSPp
dK3OgtUqZVNBq3tocxbbVw2bjxpBUN9c56tF1044UNX8H1CGSoHh8r7RR+tF1E2E
TBQtioKCdLcg2BJJrQl75/vzkEnR7Pev15ALbQnXFGGpYKwRzaBoNlQi+VwN5eQ7
i2JLTIZwfxUGTuoXcMCRiqF1fhCCMgdh1RGnt6CKOn3WyfYBSGKXjRa6ZQikaKKB
LQIDAQAB
-----END PUBLIC KEY-----";

        Assert.IsTrue(PublicKeySignature.TryParse(k, out var v));

        Assert.AreEqual(CryptoAlgorithm.Rsa, v.Algorithm);
        Assert.AreEqual(2, v.GetValues().Count);
        Assert.AreEqual((BigInteger)65537, v.GetValues()[1]);
    }
}
