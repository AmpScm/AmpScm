using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SecurityBucketTests;

[TestClass]
public class PgpKeyTypeTests
{
    private const string BrainPoolPublicKey =
@"-----BEGIN PGP PUBLIC KEY BLOCK-----

mFMEYrQs3hMJKyQDAwIIAQEHAgMEqcci+ScthIqfKrkAazlNMJvTLsciXE/VHYE8
ZiHTLrkNRF7/DPldmvwtM4Sf+9FG0TZS/OhbC6M93XHgzK4gCLQOQnJhaW5wb29s
IFRlc3SIlAQTEwgAPBYhBELRN3bbq1+xMz5UfccVQ/pHsgnQBQJitCzeAhsDBQsJ
CAcCAyICAQYVCgkICwIEFgIDAQIeBwIXgAAKCRDHFUP6R7IJ0NjAAP4gxv/cDVqX
gF+vSjXRPLVcXeUNn53lFF62AST8/rQIvAD/To4olhqFuG4SAbRtZlIFhDRTtXVI
KrLFxpx1N3OFUz64VwRitCzeEgkrJAMDAggBAQcCAwSY1TzwcUlHDMXhWQUoz0xR
uO3eAqNP5ErFqvEDlvQZACLpmRMOpoC6uMFXNy+U+tLapXYVIsH4DvhuY3lxYXxL
AwEIB4h4BBgTCAAgFiEEQtE3dturX7EzPlR9xxVD+keyCdAFAmK0LN4CGwwACgkQ
xxVD+keyCdBILAD9GmdKohE5vaPZvTUtSy8jywNBUeRz8TG92b4Ff+0OjnQA/3yD
qkbEJetaOwZOhuUqhD6TheF+osYF/dVGpvInWmVA
=SDS7
-----END PGP PUBLIC KEY BLOCK-----";
    private const string BrainPoolSignature =
@"-----BEGIN PGP SignaturePublicKey-----

iHUEABMIAB0WIQRC0Td226tfsTM+VH3HFUP6R7IJ0AUCYrQvrQAKCRDHFUP6R7IJ
0OuNAP4wWVssrQDsncN9ZwbwgBTx+SZK7t0BGy/HO0ejPdHiJgD/TKvX38ZVSvzt
R74grCkxsuX711oRA0zFfP30qi/UzDM=
=A/cR
-----END PGP SignaturePublicKey-----";

    [TestMethod]
    public async Task VerifyPgpBrainPool()
    {
#if NET6_0_OR_GREATER
        if (OperatingSystem.IsMacOS())
            Assert.Inconclusive("MacOS doesn't appear to support Brainpoool ");
#else
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            Assert.Inconclusive("");
#endif

        var src = Bucket.Create.FromASCII("test");

        var rdx = new Radix64ArmorBucket(Bucket.Create.FromASCII(BrainPoolSignature));
        using var gpg = new SignatureBucket(rdx);


        Assert.IsTrue(PublicKeySignature.TryParse(BrainPoolPublicKey, out var key));

        var fp = await gpg.ReadFingerprintAsync();
        Assert.IsNotNull(fp, "Have fingerprint");

        Assert.AreEqual(CryptoAlgorithm.Ecdsa, key.Algorithm);
        Assert.AreEqual("42D13776DBAB5FB1333E547DC71543FA47B209D0", key.FingerprintString);

        Assert.IsTrue(fp.Span.SequenceEqual(key.Fingerprint.ToArray()), "Fingerprints match");

        var ok = await gpg.VerifyAsync(src, key);
        Assert.IsTrue(ok, "BrainPool");
    }

    private const string DsaAlgamelPublicKey =
@"-----BEGIN PGP PUBLIC KEY BLOCK-----

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
68JESaa1uXk8ZZRBvp0uS3TS
=yjoG
-----END PGP PUBLIC KEY BLOCK-----";
    private const string DsaAlgamelSignature =
@"-----BEGIN PGP SignaturePublicKey-----

iHUEABEIAB0WIQSQD/u2BUfUhLOko9t+kcTtGH3EMgUCYrV53wAKCRB+kcTtGH3E
Ms22AP4h4s6JFBFdREqft2CK8DHvCwBdFe3YvDeeONKkJCg3WgD/TyXLRVa916IL
uVSFjzSWAUjZAvjV9ig9a9f6bFNOtZQ=
=RlHU
-----END PGP SignaturePublicKey-----";

#if !NETFRAMEWORK
    [TestMethod]
#endif
    public async Task VerifyPgpDsaAlgamel3072()
    {
        var src = Bucket.Create.FromASCII("test");

        var rdx = new Radix64ArmorBucket(Bucket.Create.FromASCII(DsaAlgamelSignature));
        using var gpg = new SignatureBucket(rdx);

        Assert.IsTrue(PublicKeySignature.TryParse(DsaAlgamelPublicKey, out var key));

        var fp = await gpg.ReadFingerprintAsync();
        Assert.IsNotNull(fp, "Have fingerprint");

        Assert.AreEqual(CryptoAlgorithm.Dsa, key.Algorithm);
        Assert.AreEqual("900FFBB60547D484B3A4A3DB7E91C4ED187DC432", key.FingerprintString);

        Assert.IsTrue(fp.Span.SequenceEqual(key.Fingerprint.ToArray()), "Fingerprints match");

        // This tests a 3072 bit DSA key, while mono and legacy .net
        // only supports the old 1024 bit keys, like those used in SSH.

#if !NET5_0_OR_GREATER
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            Assert.Inconclusive("DSA hash length issue on mono");
#else
        if (OperatingSystem.IsMacOS())
            Assert.Inconclusive("MacOS doesn't like 3072 bit key");
#endif

        var ok = await gpg.VerifyAsync(src, key);
        Assert.IsTrue(ok, "Dha-Algamel");
    }

    private const string Ecc25519PublicKey =
@"-----BEGIN PGP PUBLIC KEY BLOCK-----

mDMEYrLREBYJKwYBBAHaRw8BAQdAVr1b5ohYyw/5zQJoJe9qKQKYidrej30SlobO
XyuHcou0I0VDQy1DdXJ2ZTI1NTE5LVRlc3QgPHQtZWNjQGxwdDEubmw+iJQEExYK
ADwWIQQaPoMZbPR0oKyzHviIOdnGw+9H8QUCYrLREAIbAwULCQgHAgMiAgEGFQoJ
CAsCBBYCAwECHgcCF4AACgkQiDnZxsPvR/GOnwEA/CfUnDoTx/5msrw4lvFFPvEJ
4xrrl3k69ZK+hMdO3ysA/jgnCdnAhLfzytL/rU7NTTfuocf29SU39zbbYsB6uuAC
uDgEYrLREBIKKwYBBAGXVQEFAQEHQBuToLPwS5CS7rwNAaIjZk2pSuebqBpMn2QF
xC/lNOkFAwEIB4h4BBgWCgAgFiEEGj6DGWz0dKCssx74iDnZxsPvR/EFAmKy0RAC
GwwACgkQiDnZxsPvR/Fs4AEAh388vGcQ0RV3266eaGd6xzqSXGbOKH7q+/A5dXrh
6nIA/3sIBw1lMn207CM+zGp1U0uTX3KzTI2Jv97ywIHcqvsA
=0TNE
-----END PGP PUBLIC KEY BLOCK-----
";
    private const string Ecc25519Signature =
@"-----BEGIN PGP SignaturePublicKey-----

iHUEABYKAB0WIQQaPoMZbPR0oKyzHviIOdnGw+9H8QUCYrLSJAAKCRCIOdnGw+9H
8YHDAQCPKPp40I5xn5GTI5dwMrWWVasSZF0ZXEmLUn9DVY7cYAEA8algpEpMoX21
Zt5h6i/BNNX9AiqbRo/ep/RrPpU71Qo=
=WK9t
-----END PGP SignaturePublicKey-----";

    [TestMethod]
    public async Task VerifyPgpEcc25519()
    {
        var src = Bucket.Create.FromASCII("test");

        var rdx = new Radix64ArmorBucket(Bucket.Create.FromASCII(Ecc25519Signature));
        using var gpg = new SignatureBucket(rdx);

        Assert.IsTrue(PublicKeySignature.TryParse(Ecc25519PublicKey, out var key));

        var fp = await gpg.ReadFingerprintAsync();
        Assert.IsNotNull(fp, "Have fingerprint");

        Assert.AreEqual("1A3E83196CF474A0ACB31EF88839D9C6C3EF47F1", key.FingerprintString);

        Assert.IsTrue(fp.Span.SequenceEqual(key.Fingerprint.ToArray()), "Fingerprints match");

        var ok = await gpg.VerifyAsync(src, key);
        Assert.IsTrue(ok, "Ed25519");
    }

    private const string EccNistPublicKey =
@"-----BEGIN PGP PUBLIC KEY BLOCK-----

mG8EYrLYWxMFK4EEACIDAwQ3dqzKcuXivDHQy8P8e382xyQyP/tpPBIwh9ZmMJkv
nCs1cTqbqcGqmBywkwwCJfAhdQsvLdq4uJpfXFaDAqpE+pVRfCcWlic4zYH3Oztn
ieXr7okw3lnCcm6xIqDWZey0GEVDQyBUZXN0IDx0LWVjY0BscHQxLm5sPoi0BBMT
CQA8FiEESJYnnkMWF8Xvli3+gWMSp+BqHh4FAmKy2FsCGwMFCwkIBwIDIgIBBhUK
CQgLAgQWAgMBAh4HAheAAAoJEIFjEqfgah4e+7kBf1ttFFAV25laFBSmyxvivEE2
em8B/Iba5XEIaz2AmS/qeYS235DX8oLLNT0QJe8bZAF/RqmDG3dm5c50vHTHNGJ2
Bhs3pogw+bc42EJAZgAnh4UiFKHD5CnTL13ql4PJyACLuHMEYrLYWxIFK4EEACID
AwSQ09tAIUcF+4L2kIT/nHs/R2SUZFOFeK9ka7yfIRmuiG5rWl6qPV4U6ogoRRiT
6CUL/u/6J4y2I4xZGQdAAtsZK5g4ZO+S3WzVch6MGlWVKeduAPgKPQHVbpll9J/V
m/wDAQkJiJgEGBMJACAWIQRIlieeQxYXxe+WLf6BYxKn4GoeHgUCYrLYWwIbDAAK
CRCBYxKn4GoeHhZjAYDF2ALJVHTRUscewYgcVa68QM3bYSFNuyCFBYWGTTYNeBKE
rNA7FxH2eo4HO2YdP2QBgMgBeD345QWv1+pB43sFxkkt0K4h71dLd39tKZZT+Oep
rkyJCMgja4VUwipJDfpHhQ==
=zkVy
-----END PGP PUBLIC KEY BLOCK-----
";
    private const string EccNistSignature =
@"-----BEGIN PGP SignaturePublicKey-----

iJUEABMJAB0WIQRIlieeQxYXxe+WLf6BYxKn4GoeHgUCYrLYwwAKCRCBYxKn4Goe
Hn03AX4k4O0nldx2IRIHp5QSp3zJyND6cnFB5FaAgd5sTYmU7aR+5slupkS9MSrE
sMuzbXABgIX+gVot02mQzgxIvluhvnUNczvZ+QSzjT8iWHYzBWgG3zbaDzdRfvCr
izD0ZbH6Qw==
=tAmr
-----END PGP SignaturePublicKey-----
";

    [TestMethod]
    public async Task VerifyPgpEccNist()
    {
#if !NET6_0_OR_GREATER
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                Assert.Inconclusive("");
#endif

        var src = Bucket.Create.FromASCII("test");

        var rdx = new Radix64ArmorBucket(Bucket.Create.FromASCII(EccNistSignature));
        using var gpg = new SignatureBucket(rdx);

        Assert.IsTrue(PublicKeySignature.TryParse(EccNistPublicKey, out var key));

        var fp = await gpg.ReadFingerprintAsync();
        Assert.IsNotNull(fp, "Have fingerprint");

        Assert.AreEqual(CryptoAlgorithm.Ecdsa, key.Algorithm);
        Assert.AreEqual("4896279E431617C5EF962DFE816312A7E06A1E1E", key.FingerprintString);

        Assert.IsTrue(fp.Span.SequenceEqual(key.Fingerprint.ToArray()), "Fingerprints match");

        var ok = await gpg.VerifyAsync(src, key);
        Assert.IsTrue(ok, "EccNist");
    }

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
    public async Task VerifySSHRSa()
    {
        var src = Bucket.Create.FromASCII("test");

        var rdx = new Radix64ArmorBucket(Bucket.Create.FromASCII(SshSig));
        using var gpg = new SignatureBucket(rdx);

        Assert.IsTrue(PublicKeySignature.TryParse("ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQD+aILyycyvtatnMCeyucurSCkXPcuML/bgJOHFCwjeFA8CAXBlOEIgMblj7II8SpKP6mkDEFgXQXAFF6i00UAu8/NBEcW1xdLh0/12X2wlbZIPacMbGI/JbaYD5kznyDhsdyMmMfpbiZIOXPomoVbqcXh8wRqENqu1OTL5zIQBJBflEeZVG4X6oRDquklhrAcna9yPzgqwGHAQTtXpV4n//6bVzus9mTskCNFi+cKvmqifFVAwiG328VkGIrKj/EkB4MB37mIZKYn6Od+0vvzwH+1B5iW66Tjo/A2fx65spqolPLhFR2bmRSxP4Lxsg4R+TBmMDMsDPgeJfD6Iu0efs1s2rqL9VGbVC0Fgw4ds9qCfv2rLO2a+9/U5oopXbgUBlRDXiiX/zer3AFuyieWI3jevrFbRQqhlm08aWM/CP3wrpYikemXydHuxv5YUwJRaCmedOIsz5tVftfD3E+cEOIoLUTkoZwt9Ygywo+pccbTjRFouOcCT1Ib0g+6zgE0= me@pc", out var pk));

        var ok = await gpg.VerifyAsync(src, pk);
        Assert.IsTrue(ok, "RSA ok");
    }
}
