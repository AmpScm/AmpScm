using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AmpScm;
using System.Diagnostics;

namespace SecurityBucketTests;

[TestClass]
public class SshTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [DataRow("rsa", "")]
    [DataRow("rsa", "-b1024")]
    [DataRow("rsa", "-b4096")]
    [DataRow("dsa", "")]
    [DataRow("ecdsa", "")]
    [DataRow("ecdsa", "-b256")]
    [DataRow("ecdsa", "-b384")]
#if DEBUG
    [DataRow("ecdsa", "-b521")] // Typically fails on GitHub bots
#endif
    [DataRow("ed25519", "")]
    public async Task VerifySshSsh(string type, string ex)
    {
#if NETFRAMEWORK
        if (type == "ecdsa" && Environment.OSVersion.Platform != PlatformID.Win32NT)
            Assert.Inconclusive("");
#endif
        var dir = TestContext.PerTestDirectory(type + ex);

        string keyFile = Path.Combine(dir, "key");
        if (string.IsNullOrEmpty(ex))
            RunSshKeyGen("-f", keyFile, "-t", type, "-N", "");
        else
            RunSshKeyGen("-f", keyFile, "-t", type, "-N", "", ex);

        string privateKey = File.ReadAllText(keyFile).Trim();

        string publicKey = File.ReadAllText(keyFile + ".pub").Trim();
        Trace.WriteLine($"Public key: {publicKey}");

        Assert.IsTrue(PublicKeySignature.TryParse(publicKey, out var k));

        int n = publicKey.IndexOf(' ', 32);
        Assert.AreEqual(publicKey.Substring(0, n), k.FingerprintString);
        string testData = Guid.NewGuid().ToString();
        string testDataFile = Path.Combine(dir, "testdata");
        File.WriteAllText(testDataFile, testData);

        RunSshKeyGen("-Y", "sign", "-n", "ns", "-f", keyFile, testDataFile);

        string signature = File.ReadAllText(testDataFile + ".sig");

        var src = Bucket.Create.FromASCII(testData);
        var rdx = new Radix64ArmorBucket(Bucket.Create.FromASCII(signature));
        using var gpg = new SignatureBucket(rdx);

        var fp = await gpg.ReadFingerprintAsync();

        var ok = await gpg.VerifyAsync(src, k);

        Assert.IsTrue(ok, "SignaturePublicKey valid");

        Assert.IsTrue(fp.Span.SequenceEqual(k.Fingerprint.ToArray()), "Fingerprints match");

#if !NETFRAMEWORK
        Trace.WriteLine($"Result: SHA256:{Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(fp.ToArray())).TrimEnd('=')}");
#endif
    }

    [TestMethod]
    [DataRow("rsa", "")]
    [DataRow("rsa", "-b1024")]
    [DataRow("rsa", "-b4096")]
    [DataRow("dsa", "")]
    [DataRow("ecdsa", "")]
    [DataRow("ecdsa", "-b256")]
    [DataRow("ecdsa", "-b384")]
#if DEBUG
    [DataRow("ecdsa", "-b521")]
#endif
    // ed25519 can't be expressed as PEM
    public void VerifySshPem(string type, string ex)
    {
#if !NETFRAMEWORK
        if (OperatingSystem.IsMacOS())
            Assert.Inconclusive("GitHub's MacOS install's openssh doesn't like this test");
#else
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            Assert.Inconclusive("GitHub's MacOS install's openssh doesn't like this test");
#endif

        var dir = TestContext.PerTestDirectory(type + ex);

        string keyFile = Path.Combine(dir, "key");
        if (string.IsNullOrEmpty(ex))
            RunSshKeyGen("-f", keyFile, "-t", type, "-N", "");
        else
            RunSshKeyGen("-f", keyFile, "-t", type, "-N", "", ex);


        string publicKeyPem = RunSshKeyGen("-e", "-f", $"{keyFile}.pub", "-m", "pem");
        string publicKeyRfc4716 = RunSshKeyGen("-e", "-f", $"{keyFile}.pub", "-m", "RFC4716");


        string publicKey = File.ReadAllText(keyFile + ".pub").Trim();
        Assert.IsTrue(PublicKeySignature.TryParse(publicKey, out var k), "Parse public key");

        Assert.IsTrue(PublicKeySignature.TryParse(publicKeyPem, out var kPem), "Parse public key pem");

        Assert.IsTrue(PublicKeySignature.TryParse(publicKeyRfc4716, out var kRfc4716), "Parse");

        Assert.AreEqual(k.Algorithm, kPem.Algorithm, "Pem algorithm");
        Assert.AreEqual(k.Algorithm, kRfc4716.Algorithm, "4716 alg");

        Assert.AreEqual(k.GetValues(true).Count, kPem.GetValues().Count, "pem value count");
        Assert.AreEqual(k.GetValues(true).Count, kRfc4716.GetValues().Count, "4716 value count");

        for (int i = 0; i < k.GetValues().Count; i++)
        {
            Assert.AreEqual(k.GetValues()[i], kPem.GetValues()[i], $"Values of pem[{i}] match");
            Assert.AreEqual(k.GetValues()[i], kRfc4716.GetValues()[i], $"Values of 4716[{i}] match");
        }

        Trace.WriteLine($"From SSH:     {k.FingerprintString}");
        Trace.WriteLine($"From PEM:     {kPem.FingerprintString}");
        Trace.WriteLine($"From Rfc4716: {kRfc4716.FingerprintString}");

        if (type != "ecdsa")
            Assert.AreEqual(k.FingerprintString, kPem.FingerprintString, "pem fingerprint");
        Assert.AreEqual(k.FingerprintString, kRfc4716.FingerprintString, "4716 fingerprint");

        if (type != "ecdsa")
            Assert.IsTrue(k.Fingerprint.SequenceEqual(kPem.Fingerprint), "pem fingerprint");
        Assert.IsTrue(k.Fingerprint.SequenceEqual(kRfc4716.Fingerprint), "4716 fingerprint");

#if !NETFRAMEWORK
        Trace.WriteLine($"Result: SHA256:{Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(k.Fingerprint.Span)).TrimEnd('=')}");
#endif
    }

    private string RunSshKeyGen(params string[] args)
    {
        try
        {
            return TestContext.RunApp("ssh-keygen", args);
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"ssh-keygen failed: {ex.Message}");
            throw;
        }
    }
}
