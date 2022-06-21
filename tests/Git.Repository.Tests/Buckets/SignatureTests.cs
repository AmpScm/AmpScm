using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmpScm;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Git.Objects;
using AmpScm.Buckets.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Diagnostics;

namespace GitRepositoryTests.Buckets
{
    [TestClass]
    public class SignatureTests
    {
        public TestContext TestContext { get; set; } = default!;

        const string Sig1 =
@"-----BEGIN PGP SIGNATURE-----

wsBcBAABCAAQBQJioEepCRBK7hj4Ov3rIwAAGoMIAHCHbTas3gShVMMX2dx2r82B
33bY2C8sQ+jFyrJHid8Kq8CMokk2cgPNfELyUw/Sjce4M/CxWq6didx58OOg6nom
XIPqvRsHqFuNpuC0Ku9vW4fuiITD6i8ADPpNwsU2lVVSqwVdmdBCU5PncXyYk3Bs
0P1rgF80R9NepCqZ0FihrscJMqX72F5xiq+EGAa/fz4QWDpi792B1fOOO3Q412R6
KJWsEX/HpigGCiiQ/BHf/z3hj9URNEKOWd2hRAJsvkYzczhV8/yZmSjlg9kUp/Cw
aWhgukgOUppFsmnAfSp4zz0MmV2vbAKJQrrTmi1PmDFXt/mDv5xCifZpWbS46cY=
=7fmL
-----END PGP SIGNATURE-----";

        const string Sig2 = // Similar as sig1 but whitespace at start of line
@"-----BEGIN PGP SIGNATURE-----

 wsBcBAABCAAQBQJimgGPCRBK7hj4Ov3rIwAAP10IAGkgEDtRaPWlyreQincqo8KM
 vO5uh/G1JzqO1fxtwfzjJB/u48/c/brHqimIEug76zA6vEkkE9Cl42qNY+0vDeII
 MQFED+td5vxJ1lHchkZDcQg+fASmAi0XfD2FfEXbQgwH80dIITcUTGlCySr76M9o
 pqpK8n1PpJXtWnCpj13J/3G5Ugo//H0YUqZJFedz36RxuKw1W7WwZgCtCUd6xNz+
 cZs0jUC2gwbMTD5sfBOGUVMKTANFKy+4gda3ouCPyAP+ptFIT10LbWptsoLnYgx8
 oJzt9PpjzQpPRp9baotmzN72sIHjh5bMqJ9HpUK/RR6FLUSO0qwi54xxL8RckZ4=
 =37hr
 -----END PGP SIGNATURE-----";

        const string SigDSA =
@"-----BEGIN PGP SIGNATURE-----

iIIEABEIACoWIQTHNE8vTjJt+Sudk9934a70jlWGXwUCYrAsPwwcZHNhQGxwdDEu
bmwACgkQd+Gu9I5Vhl/hfgD/XmXduRrXvp8wD7cuKWkKfotF+IIgtCnC7FMf9Eq1
WukA/jvr/XbHcqQmFzmWYxf+k3Q5eqKGtMka41jfCWCPxt0Y
=ofhh
-----END PGP SIGNATURE-----";

        // https://superuser.com/questions/308126/is-it-possible-to-sign-a-file-using-an-ssh-key
        const string SshSig =
@"-----BEGIN SSH SIGNATURE-----
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
-----END SSH SIGNATURE-----";

        //        const string sshSig2 =
        //@"-----BEGIN SSH SIGNATURE-----
        //U1NIU0lHAAAAAQAAADMAAAALc3NoLWVkMjU1MTkAAAAgJKxoLBJBivUPNTUJUSslQTt2hD
        //jozKvHarKeN8uYFqgAAAADZm9vAAAAAAAAAFMAAAALc3NoLWVkMjU1MTkAAABAKNC4IEbt
        //Tq0Fb56xhtuE1/lK9H9RZJfON4o6hE9R4ZGFX98gy0+fFJ/1d2/RxnZky0Y7GojwrZkrHT
        //FgCqVWAQ==
        //-----END SSH SIGNATURE-----";

        [TestMethod]
        [DataRow(Sig1, DisplayName = nameof(Sig1))]
        [DataRow(Sig2, DisplayName = nameof(Sig2))]
        [DataRow(SigDSA, DisplayName = nameof(SigDSA))]
        [DataRow(SshSig, DisplayName = nameof(SshSig))]
        //[DataRow(sshSig2, DisplayName = nameof(sshSig2))]
        public async Task ParseSignature(string signature)
        {
            var b = Bucket.Create.FromASCII(signature);

            using var sr = new Radix64ArmorBucket(b);

            while (true)
            {
                var bb = await sr.ReadHeaderAsync();
                if (bb.IsEof)
                    break;
            }

            var dt = await sr.ReadExactlyAsync(Bucket.MaxRead);
        }

        [TestMethod]
        [DataRow(Sig1, DisplayName = nameof(Sig1))]
        [DataRow(Sig2, DisplayName = nameof(Sig2))]
        [DataRow(SigDSA, DisplayName = nameof(SigDSA))]
        [DataRow(SshSig, DisplayName = nameof(SshSig))]
        //[DataRow(sshSig2, DisplayName = nameof(sshSig2))]
        public async Task ParseSigTail(string signature)
        {
            var b = Bucket.Create.FromASCII(signature + Environment.NewLine + "TAIL!");

            using var sr = new Radix64ArmorBucket(b);

            while (true)
            {
                var bb = await sr.ReadHeaderAsync();
                if (bb.IsEof)
                    break;
            }

            var dt = await sr.ReadExactlyAsync(Bucket.MaxRead);

            var bt = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("TAIL!", bt.ToASCIIString());
        }

        [TestMethod]
        [DataRow(Sig1, DisplayName = nameof(Sig1))]
        [DataRow(Sig2, DisplayName = nameof(Sig2))]
        [DataRow(SigDSA, DisplayName = nameof(SigDSA))]
        [DataRow(SshSig, DisplayName = nameof(SshSig))]
        //[DataRow(sshSig2, DisplayName = nameof(sshSig2))]
        public async Task ParseRfc4880(string signature)
        {
            var b = Bucket.Create.FromASCII(signature + Environment.NewLine + "TAIL!");

            var sr = new Radix64ArmorBucket(b);
            using var rr = new GitSignatureBucket(sr);

            var bb = await rr.ReadExactlyAsync(8192);

            //Assert.AreEqual(OpenPgpTagType.Signature, await rr.ReadTagAsync());

            var bt = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("TAIL!", bt.ToASCIIString());
        }

        const string MergeTag =
@"tree 31b100c21e7d04fab9a2ce69b192f40798f2f260
parent 233087ca063686964a53c829d547c7571e3f67bf
parent ba7542eb2dd5dfc75c457198b88986642e602065
author Linus Torvalds <torvalds@linux-foundation.org> 1651079692 -0700
committer Linus Torvalds <torvalds@linux-foundation.org> 1651079692 -0700
mergetag object ba7542eb2dd5dfc75c457198b88986642e602065
 type commit
 tag mtd/fixes-for-5.18-rc5
 tagger Miquel Raynal <miquel.raynal@bootlin.com> 1651051974 +0200
 
 MTD core fix:
 * Fix a possible data corruption of the 'part' field in mtd_info
 
 Rawnand fixes:
 * Fix the check on the return value of wait_for_completion_timeout
 * Fix wrong ECC parameters for mt7622
 * Fix a possible memory corruption that might panic in the Qcom driver
 -----BEGIN PGP SIGNATURE-----
 
 iQEzBAABCgAdFiEE9HuaYnbmDhq/XIDIJWrqGEe9VoQFAmJpD0cACgkQJWrqGEe9
 VoRjRwgAj31KV9OhfKupvj5QWqsB/06kyKElkJJJFibLPT/tPlS5iY6o/UMUlCAy
 hUw8yYMOrSN2JySYHiDvifJBbRZPrJmAnY8CkC5uvCx7E9yzRpSbmdSSZTjKk2S0
 4eml5JSV0iVKIX5sZ6CMMKZMB9b2peoWi9ebT+hBYndof8CT8d8cki0Yj/W/DGVA
 +PuT6l4zJNII8NsrR+lGTyCTiw2DUuUn0sudBCj+LU7gBl16bD4Oxj9s2c6N6TuC
 grze+yeq0AqntS2gaiIsmRHShd/gpTfd4OAYkifpvclUYMGlfy2i038eTsuLLMfA
 /mUa/sEPQkhG5TlHZvNtHf73QBVK3g==
 =gnxP
 -----END PGP SIGNATURE-----

Merge tag 'mtd/fixes-for-5.18-rc5' of git://git.kernel.org/pub/scm/linux/kernel/git/mtd/linux

Pull MTD fixes from Miquel Raynal:
 ""Core fix:

   - Fix a possible data corruption of the 'part' field in mtd_info

  Rawnand fixes:

   - Fix the check on the return value of wait_for_completion_timeout

   - Fix wrong ECC parameters for mt7622

   - Fix a possible memory corruption that might panic in the Qcom
     driver""

* tag 'mtd/fixes-for-5.18-rc5' of git://git.kernel.org/pub/scm/linux/kernel/git/mtd/linux:
  mtd: rawnand: qcom: fix memory corruption that causes panic
  mtd: fix 'part' field data corruption in mtd_info
  mtd: rawnand: Fix return value check of wait_for_completion_timeout
  mtd: rawnand: fix ecc parameters for mt7622
";

        [TestMethod]
        public async Task ReadMergeTag()
        {
            var src = Bucket.Create.FromASCII(MergeTag.Replace("\r", ""));
            bool readTag = false;
            bool readSig = false;

            using var commitReader = new GitCommitObjectBucket(src, handleSubBucket);

            await commitReader.ReadUntilEofAsync();

            Assert.IsTrue(readTag, "Read tag");
            Assert.IsTrue(readSig, "Read signature");

            async ValueTask handleSubBucket(GitSubBucketType subBucket, Bucket bucket)
            {
                if (subBucket == GitSubBucketType.MergeTag)
                {
                    using var gto = new GitTagObjectBucket(bucket, handleSubBucket);

                    await gto.ReadUntilEofAsync();
                    readTag = true;
                }
                else if (subBucket == GitSubBucketType.Signature)
                {
                    using var gto = new Radix64ArmorBucket(bucket);

                    await gto.ReadUntilEofAsync();
                    readSig = true;
                }
                else
                    await bucket.ReadUntilEofAndCloseAsync();
            }
        }

        const string SignedCommit =
@"tree d6b79075cd65b101cec0b38f9e4eb86de5b850a7
parent 660e6bd5cd277296aa8b3aadc1383995b6c00e87
parent 0f5944459beb952fd49461dfb3c2867de7df314b
author Edward Thomson <ethomson@edwardthomson.com> 1655044812 -0400
committer GitHub <noreply@github.com> 1655044812 -0400
gpgsig -----BEGIN PGP SIGNATURE-----
 
 wsBcBAABCAAQBQJipfrMCRBK7hj4Ov3rIwAAJIcIABCqxS+DdDQh9iIIaNFvI4Ul
 djcR9XeHfK9PrbGZFTKLmSj0WdU6JNplBiNl0IsvAEPatAKMKc+J6kz1o4gJHNIq
 gT5dQQ8lKiVTq+adMwU1ikAreh6/a8jMCFImXrQchAZjWn0CK7DZxWmQ1iS+VgmZ
 kNP0DQqxg4qPYgdyaGLh6+jWMOJqMYbOBViJMd6xN1vgyX9pqXKzkTPv0OyY4GqM
 5l2Munaeoq36powq+grP8f7zLFucfi+HL71EJKRGrMta0UBgR0ncJHQO1Qtbzypx
 7DPWGlcyfCo3+pPK8BsqHKj800StyhuNe5gsH6cSEI94XnH3eUxZG64S6N/cBxs=
 =2xQs
 -----END PGP SIGNATURE-----
 

Merge pull request #6288 from libgit2/cmn/mwindow-simplifications

A couple of simplications around mwindow";
        [TestMethod]
        public async Task ReadSignedCommit()
        {
            var src = Bucket.Create.FromASCII(SignedCommit.Replace("\r", ""));
            bool readGpg = false;

            var verifySrcReader = GitCommitObjectBucket.ForSignature(src.Duplicate()).Buffer();
            using var commitReader = new GitCommitObjectBucket(src, handleSubBucket);

            await commitReader.ReadUntilEofAsync();

            Assert.IsTrue(readGpg);

            async ValueTask handleSubBucket(GitSubBucketType subBucket, Bucket bucket)
            {
                if (subBucket != GitSubBucketType.Signature)
                    await bucket.ReadUntilEofAndCloseAsync();

                var rdx = new Radix64ArmorBucket(bucket);
                using var gpg = new GitSignatureBucket(rdx);

                var ok1 = await gpg.VerifyAsync(verifySrcReader.NoClose(), null);
                Assert.IsTrue(ok1, "Verify as signature from 'someone'");

                verifySrcReader.Reset();

                var ok = await gpg.VerifyAsync(verifySrcReader, await GetGitHubWebFlowKey());
                Assert.IsTrue(ok, "Valid sigature");
                readGpg = true;
            }
        }

        const string SignedTag =
@"object ba7542eb2dd5dfc75c457198b88986642e602065
type commit
tag mtd/fixes-for-5.18-rc5
tagger Miquel Raynal <miquel.raynal@bootlin.com> 1651051974 +0200

MTD core fix:
* Fix a possible data corruption of the 'part' field in mtd_info

Rawnand fixes:
* Fix the check on the return value of wait_for_completion_timeout
* Fix wrong ECC parameters for mt7622
* Fix a possible memory corruption that might panic in the Qcom driver
-----BEGIN PGP SIGNATURE-----

iQEzBAABCgAdFiEE9HuaYnbmDhq/XIDIJWrqGEe9VoQFAmJpD0cACgkQJWrqGEe9
VoRjRwgAj31KV9OhfKupvj5QWqsB/06kyKElkJJJFibLPT/tPlS5iY6o/UMUlCAy
hUw8yYMOrSN2JySYHiDvifJBbRZPrJmAnY8CkC5uvCx7E9yzRpSbmdSSZTjKk2S0
4eml5JSV0iVKIX5sZ6CMMKZMB9b2peoWi9ebT+hBYndof8CT8d8cki0Yj/W/DGVA
+PuT6l4zJNII8NsrR+lGTyCTiw2DUuUn0sudBCj+LU7gBl16bD4Oxj9s2c6N6TuC
grze+yeq0AqntS2gaiIsmRHShd/gpTfd4OAYkifpvclUYMGlfy2i038eTsuLLMfA
/mUa/sEPQkhG5TlHZvNtHf73QBVK3g==
=gnxP
-----END PGP SIGNATURE-----
";

        const string PublicKeyOfSignedTag =
@"-----BEGIN PGP PUBLIC KEY BLOCK-----

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
=bYqd
-----END PGP PUBLIC KEY BLOCK-----
";

        static async ValueTask<GitPublicKey> GetKey()
        {
            var key = Bucket.Create.FromASCII(PublicKeyOfSignedTag.Replace("\r", ""));

            var radix = new Radix64ArmorBucket(key);
            var kb = new GitSignatureBucket(radix);

            await kb.ReadUntilEofAndCloseAsync();
            return await kb.ReadKeyAsync();
        }

        const string WebFlowKey =
@"-----BEGIN PGP PUBLIC KEY BLOCK-----

xsBNBFmUaEEBCACzXTDt6ZnyaVtueZASBzgnAmK13q9Urgch+sKYeIhdymjuMQta
x15OklctmrZtqre5kwPUosG3/B2/ikuPYElcHgGPL4uL5Em6S5C/oozfkYzhwRrT
SQzvYjsE4I34To4UdE9KA97wrQjGoz2Bx72WDLyWwctD3DKQtYeHXswXXtXwKfjQ
7Fy4+Bf5IPh76dA8NJ6UtjjLIDlKqdxLW4atHe6xWFaJ+XdLUtsAroZcXBeWDCPa
buXCDscJcLJRKZVc62gOZXXtPfoHqvUPp3nuLA4YjH9bphbrMWMf810Wxz9JTd3v
yWgGqNY0zbBqeZoGv+TuExlRHT8ASGFS9SVDABEBAAHNNUdpdEh1YiAod2ViLWZs
b3cgY29tbWl0IHNpZ25pbmcpIDxub3JlcGx5QGdpdGh1Yi5jb20+wsBiBBMBCAAW
BQJZlGhBCRBK7hj4Ov3rIwIbAwIZAQAAmQEIACATWFmi2oxlBh3wAsySNCNV4IPf
DDMeh6j80WT7cgoX7V7xqJOxrfrqPEthQ3hgHIm7b5MPQlUr2q+UPL22t/I+ESF6
9b0QWLFSMJbMSk+BXkvSjH9q8jAO0986/pShPV5DU2sMxnx4LfLfHNhTzjXKokws
+8ptJ8uhMNIDXfXuzkZHIxoXk3rNcjDN5c5X+sK8UBRH092BIJWCOfaQt7v7wig5
4Ra28pM9GbHKXVNxmdLpCFyzvyMuCmINYYADsC848QQFFwnd4EQnupo6QvhEVx1O
j7wDwvuH5dCrLuLwtwXaQh0onG4583p0LGms2Mf5F+Ick6o/4peOlBoZz48=
=HXDP
-----END PGP PUBLIC KEY BLOCK-----";
        static async ValueTask<GitPublicKey> GetGitHubWebFlowKey()
        {
            var key = Bucket.Create.FromASCII(WebFlowKey.Replace("\r", ""));

            var radix = new Radix64ArmorBucket(key);
            var kb = new GitSignatureBucket(radix);

            await kb.ReadUntilEofAndCloseAsync();
            return await kb.ReadKeyAsync();
        }

        [TestMethod]
        public async Task ReadSignedTag()
        {
            var key = await GetKey();

            var src = Bucket.Create.FromASCII(SignedTag.Replace("\r", ""));
            bool readGpg = false;

            var verifySrcReader = GitTagObjectBucket.ForSignature(src.Duplicate()).Buffer();
            using var tagReader = new GitTagObjectBucket(src, handleSubBucket);

            await tagReader.ReadUntilEofAsync();

            Assert.IsTrue(readGpg);


            async ValueTask handleSubBucket(GitSubBucketType subBucket, Bucket bucket)
            {
                if (subBucket != GitSubBucketType.Signature)
                    await bucket.ReadUntilEofAndCloseAsync();

                var rdx = new Radix64ArmorBucket(bucket);
                using var gpg = new GitSignatureBucket(rdx);

                var ok = await gpg.VerifyAsync(verifySrcReader.NoClose(), null);
                Assert.IsTrue(ok, "Signature appears ok");

                verifySrcReader.Reset();
                ok = await gpg.VerifyAsync(verifySrcReader, key);
                Assert.IsTrue(ok, "Signature verified against signer");
                readGpg = true;
            }
        }

        [TestMethod]
        public async Task ReadPublicKey()
        {
            var key = Bucket.Create.FromASCII(PublicKeyOfSignedTag.Replace("\r", ""));

            var radix = new Radix64ArmorBucket(key);
            var kb = new GitSignatureBucket(radix);

            await kb.ReadUntilEofAndCloseAsync();
            await kb.ReadKeyAsync();
        }

        [TestMethod]
        public async Task VerifySSHRSa()
        {
            var src = Bucket.Create.FromASCII("test");

            var rdx = new Radix64ArmorBucket(Bucket.Create.FromASCII(SshSig));
            using var gpg = new GitSignatureBucket(rdx);

            //await gpg.ReadAsync();

            Assert.IsTrue(GitPublicKey.TryParse("ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQD+aILyycyvtatnMCeyucurSCkXPcuML/bgJOHFCwjeFA8CAXBlOEIgMblj7II8SpKP6mkDEFgXQXAFF6i00UAu8/NBEcW1xdLh0/12X2wlbZIPacMbGI/JbaYD5kznyDhsdyMmMfpbiZIOXPomoVbqcXh8wRqENqu1OTL5zIQBJBflEeZVG4X6oRDquklhrAcna9yPzgqwGHAQTtXpV4n//6bVzus9mTskCNFi+cKvmqifFVAwiG328VkGIrKj/EkB4MB37mIZKYn6Od+0vvzwH+1B5iW66Tjo/A2fx65spqolPLhFR2bmRSxP4Lxsg4R+TBmMDMsDPgeJfD6Iu0efs1s2rqL9VGbVC0Fgw4ds9qCfv2rLO2a+9/U5oopXbgUBlRDXiiX/zer3AFuyieWI3jevrFbRQqhlm08aWM/CP3wrpYikemXydHuxv5YUwJRaCmedOIsz5tVftfD3E+cEOIoLUTkoZwt9Ygywo+pccbTjRFouOcCT1Ib0g+6zgE0= me@pc", out var pk));

            var ok = await gpg.VerifyAsync(src, pk);
            Assert.IsTrue(ok, "RSA ok");
        }

        [TestMethod]
        [DataRow("rsa", "")]
        [DataRow("rsa", "-b1024")]
        [DataRow("rsa", "-b4096")]
        [DataRow("dsa", "")]
        [DataRow("ecdsa", "")]
        [DataRow("ecdsa", "-b256")]
        [DataRow("ecdsa", "-b384")]
        [DataRow("ecdsa", "-b521")]
        [DataRow("ed25519", "")]
        //[Timeout(5000)]
        public async Task TaskVerifyGenerateSSH(string type, string ex)
        {
#if !NET6_0_OR_GREATER
            if (type == "ecdsa" && Environment.OSVersion.Platform != PlatformID.Win32NT)
                Assert.Inconclusive("");
#endif
            var dir = TestContext.PerTestDirectory(type+ex);

            string keyFile = Path.Combine(dir, "key");
            if (string.IsNullOrEmpty(ex))
                RunSshKeyGen("-f", keyFile, "-t", type, "-N", "");
            else
                RunSshKeyGen("-f", keyFile, "-t", type, "-N", "", ex);

            string privateKey = File.ReadAllText(keyFile).Trim();
            //Console.WriteLine(privateKey);

            string publicKey = File.ReadAllText(keyFile + ".pub").Trim();
            Console.WriteLine(publicKey);

            Assert.IsTrue(GitPublicKey.TryParse(publicKey, out var k));

            string testData = Guid.NewGuid().ToString();
            string testDataFile = Path.Combine(dir, "testdata");
            File.WriteAllText(testDataFile, testData);

            RunSshKeyGen("-Y", "sign", "-n", "ns", "-f", keyFile, testDataFile);

            string signature = File.ReadAllText(testDataFile + ".sig");

            var src = Bucket.Create.FromASCII(testData);
            var rdx = new Radix64ArmorBucket(Bucket.Create.FromASCII(signature));
            using var gpg = new GitSignatureBucket(rdx);

            var ok = await gpg.VerifyAsync(src, k);

            Assert.IsTrue(ok, "Signature valid");
        }

        static void RunSshKeyGen(params string[] args)
        {
            FixConsoleEncoding();
            ProcessStartInfo psi = new ProcessStartInfo("ssh-keygen", string.Join(" ", args.Select(x => EscapeGitCommandlineArgument(x.Replace('\\', '/')))));
            Console.WriteLine(psi.Arguments);
            psi.UseShellExecute = false;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            using var p = Process.Start(psi);
            Assert.IsNotNull(p);

            p.StandardInput.Close();

            Console.WriteLine(p.StandardError.ReadToEnd());
            Console.WriteLine(p.StandardOutput.ReadToEnd());
            
            p.WaitForExit();

            Assert.AreEqual(0, p.ExitCode);
        }

        static void FixConsoleEncoding()
        {
            var ci = Console.InputEncoding;
            if (ci == Encoding.UTF8 && ci.GetPreamble().Length > 0)
            {
                // Workaround CHCP 65001 / UTF8 bug, where the process will always write a BOM to each started process
                // with Stdin redirected, which breaks processes which explicitly expect some strings as binary data
                Console.InputEncoding = new UTF8Encoding(false, true);
            }
        }

        static string EscapeGitCommandlineArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return "\"\"";

            bool escape = false;
            for (int i = 0; i < argument.Length; i++)
            {
                if (char.IsWhiteSpace(argument, i))
                {
                    escape = true;
                    break;
                }
                else if (argument[i] == '\"')
                {
                    escape = true;
                    break;
                }
            }

            if (!escape)
                return argument;

            StringBuilder sb = new StringBuilder(argument.Length + 5);

            sb.Append('\"');

            for (int i = 0; i < argument.Length; i++)
            {
                switch (argument[i])
                {
                    case '\"':
                        sb.Append('\\');
                        break;
                }

                sb.Append(argument[i]);
            }

            sb.Append('\"');

            return sb.ToString();
        }
    }
}
