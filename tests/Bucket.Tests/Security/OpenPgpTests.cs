using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Signatures;
using AmpScm.Buckets.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BucketTests.Security;

[TestClass]
public class OpenPgpTests
{
    const string rsa_key1_fingerprint = "1E4EA61FCC73D2D96075A9835B15B06B7943D080";
    const string rsa_key1 =
@"-----BEGIN PGP PRIVATE KEY BLOCK-----

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
9GPs2YhVJY76FLNUH7BPGXZ+6jo3KrQA
=8qhj
-----END PGP PRIVATE KEY BLOCK-----";

    const string rsa_key2_fingerprint = "8B34F2DCDBA7FECF88029C53C5D6650A9716C11A";
    const string rsa_key2 =
@"-----BEGIN PGP PRIVATE KEY BLOCK-----

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
=uXxW
-----END PGP PRIVATE KEY BLOCK-----
";

    string msg_from_rsa1_to_rsa2 =
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

    string msg_from_rsa1_to_rsa2_2 =
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
    string msg_from_rsa1_to_rsa2_3 =
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
        Assert.IsTrue(Signature.TryParse(rsa_key1, out var key1));
        Assert.AreEqual(rsa_key1_fingerprint, key1.FingerprintString);
        Assert.AreEqual(new MailAddress("test@rsa", "RSA Test"), key1.MailAddress);
        Assert.AreEqual(SignatureAlgorithm.Rsa, key1.Algorithm);
        Assert.IsTrue(key1.HasSecret, "Key1 has secret");

        Assert.IsTrue(Signature.TryParse(rsa_key2, out var key2));
        Assert.AreEqual(rsa_key2_fingerprint, key2.FingerprintString);
        Assert.AreEqual(new MailAddress("test-3072@rsa", "RSA Test 3072"), key2.MailAddress);
        Assert.AreEqual(SignatureAlgorithm.Rsa, key2.Algorithm);
        Assert.IsTrue(key1.HasSecret, "Key2 has secret");

        using var decr = new Radix64ArmorBucket(Encoding.ASCII.GetBytes(msg_from_rsa1_to_rsa2).AsBucket());

        using var dec = new PgpDecryptBucket(decr, _ => key2);


        var bb = await dec.ReadExactlyAsync(1024);

        Assert.AreEqual("", bb.ToUTF8String());
    }


    [TestMethod]
    public async Task TestDecrypt()
    {
        var k = File.ReadAllText(@"f:\2023\allservices_0xF5B39A58_SECRET.asc");

        var r = new Radix64ArmorBucket(Bucket.Create.FromASCII(k));
        using var sig = new SignatureBucket(r);

        var key = await sig.ReadKeyAsync();

        Assert.IsNotNull(key);
        Assert.IsNotNull(key.SubKeys.FirstOrDefault());
        Assert.AreEqual(SignatureAlgorithm.Rsa, key.Algorithm);


        Assert.AreEqual("13A75DB5332C5BF28D85749E2B9EB676F5B39A58", key.FingerprintString);

        //key.FingerprintString

        foreach (var f in Directory.GetFiles(@"f:\kpn", "*.csv.gpg"))
        {
            Trace.WriteLine(f);

            var b = File.ReadAllBytes(f);

            var fb = b.AsBucket(); // FileBucket.OpenRead(f);
            var dc = new PgpDecryptBucket(fb, _ => key);

            while (await dc.ReadExactlyUntilEolAsync(BucketEol.LF) is { } line)
            {
                if (line.Bytes.IsEof)
                    break;
                Trace.Write(line.Bytes.ToASCIIString());
            }

            await dc.ReadUntilEofAsync();
            break;
        }
    }

    [TestMethod]
    public async Task TestTestData()
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

        var b = new PgpDecryptBucket(r.AsBucket(), _ => null) { GetPassword = () => "password" };

        await b.ReadAsync();
    }

    [TestMethod]
    public async Task TestOCB()
    {
        var key = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F }.Reverse().ToArray();

        var crypted = new byte[]
        {
            0x44, 0x12, 0x92, 0x34, 0x93, 0xC5, 0x7D, 0x5D,  0xE0, 0xD7, 0x00, 0xF7, 0x53, 0xCC, 0xE0, 0xD1,
            0xD2, 0xD9, 0x50, 0x60, 0x12, 0x2E, 0x9F, 0x15,  0xA5, 0xDD, 0xBF, 0xC5, 0x78, 0x7E, 0x50, 0xB5,
            0xCC, 0x55, 0xEE, 0x50, 0x7B, 0xCB, 0x08, 0x4E,  0x47, 0x9A, 0xD3, 0x63, 0xAC, 0x36, 0x6B, 0x95,
            0xA9, 0x8C, 0xA5, 0xF3, 0x00, 0x0B, 0x14, 0x79
        };

        var d = new OcbDecodeBucket(crypted.AsBucket(), OcbDecodeBucket.SetupAes(key), 1024, 128,
            new byte[] { 0xBB, 0xAA, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x0F }, 
            new byte[] { });

        var r = await d.ReadExactlyAsync(1024);

        Assert.AreEqual(40, r.Length);
        Assert.IsTrue(new byte[] {
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,  0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,  0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
            0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, }.SequenceEqual(r.ToArray()));
    }

}
