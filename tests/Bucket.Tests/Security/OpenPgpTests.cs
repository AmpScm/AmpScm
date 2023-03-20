using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    [TestMethod]
    public void TestPrivateKeys()
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

}
