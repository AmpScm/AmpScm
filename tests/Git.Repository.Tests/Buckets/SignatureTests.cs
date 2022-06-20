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

        const string sig1 =
@"-----BEGIN PGP SIGNATURE-----

wsBcBAABCAAQBQJioEepCRBK7hj4Ov3rIwAAGoMIAHCHbTas3gShVMMX2dx2r82B
33bY2C8sQ+jFyrJHid8Kq8CMokk2cgPNfELyUw/Sjce4M/CxWq6didx58OOg6nom
XIPqvRsHqFuNpuC0Ku9vW4fuiITD6i8ADPpNwsU2lVVSqwVdmdBCU5PncXyYk3Bs
0P1rgF80R9NepCqZ0FihrscJMqX72F5xiq+EGAa/fz4QWDpi792B1fOOO3Q412R6
KJWsEX/HpigGCiiQ/BHf/z3hj9URNEKOWd2hRAJsvkYzczhV8/yZmSjlg9kUp/Cw
aWhgukgOUppFsmnAfSp4zz0MmV2vbAKJQrrTmi1PmDFXt/mDv5xCifZpWbS46cY=
=7fmL
-----END PGP SIGNATURE-----";

        const string sig2 = // Similar as sig1 but whitespace at start of line
@"-----BEGIN PGP SIGNATURE-----

 wsBcBAABCAAQBQJimgGPCRBK7hj4Ov3rIwAAP10IAGkgEDtRaPWlyreQincqo8KM
 vO5uh/G1JzqO1fxtwfzjJB/u48/c/brHqimIEug76zA6vEkkE9Cl42qNY+0vDeII
 MQFED+td5vxJ1lHchkZDcQg+fASmAi0XfD2FfEXbQgwH80dIITcUTGlCySr76M9o
 pqpK8n1PpJXtWnCpj13J/3G5Ugo//H0YUqZJFedz36RxuKw1W7WwZgCtCUd6xNz+
 cZs0jUC2gwbMTD5sfBOGUVMKTANFKy+4gda3ouCPyAP+ptFIT10LbWptsoLnYgx8
 oJzt9PpjzQpPRp9baotmzN72sIHjh5bMqJ9HpUK/RR6FLUSO0qwi54xxL8RckZ4=
 =37hr
 -----END PGP SIGNATURE-----";

        const string sigDSA =
@"-----BEGIN PGP SIGNATURE-----

iIIEABEIACoWIQTHNE8vTjJt+Sudk9934a70jlWGXwUCYrAsPwwcZHNhQGxwdDEu
bmwACgkQd+Gu9I5Vhl/hfgD/XmXduRrXvp8wD7cuKWkKfotF+IIgtCnC7FMf9Eq1
WukA/jvr/XbHcqQmFzmWYxf+k3Q5eqKGtMka41jfCWCPxt0Y
=ofhh
-----END PGP SIGNATURE-----";

        // https://superuser.com/questions/308126/is-it-possible-to-sign-a-file-using-an-ssh-key
        const string sshSig =
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
        [DataRow(sig1, DisplayName = nameof(sig1))]
        [DataRow(sig2, DisplayName = nameof(sig2))]
        [DataRow(sigDSA, DisplayName = nameof(sigDSA))]
        [DataRow(sshSig, DisplayName = nameof(sshSig))]
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
        [DataRow(sig1, DisplayName = nameof(sig1))]
        [DataRow(sig2, DisplayName = nameof(sig2))]
        [DataRow(sigDSA, DisplayName = nameof(sigDSA))]
        [DataRow(sshSig, DisplayName = nameof(sshSig))]
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
        [DataRow(sig1, DisplayName = nameof(sig1))]
        [DataRow(sig2, DisplayName = nameof(sig2))]
        [DataRow(sigDSA, DisplayName = nameof(sigDSA))]
        [DataRow(sshSig, DisplayName = nameof(sshSig))]
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

        const string mergetag =
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
            var src = Bucket.Create.FromASCII(mergetag.Replace("\r", ""));
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

        const string signedCommit =
@"tree 49bf90d07df60ac5c9ebdede5547f4a733d013cd
parent 05f211543bda96ba86bef14e8e6521069fb77797
parent cdff2f0237f663e0f68155655a8b66d05c1ec716
author Edward Thomson <ethomson@edwardthomson.com> 1655172877 -0400
committer GitHub <noreply@github.com> 1655172877 -0400
gpgsig -----BEGIN PGP SIGNATURE-----
 
 wsBcBAABCAAQBQJip+8NCRBK7hj4Ov3rIwAAw9cIACWyL7ApK82WkscXLl60uOqK
 o1B6DFIaQfg9DyFPpIb0QU+SPaPBlLNbYgck0tJFbL2fzIBPyLBxeVXUsKFuVv2D
 J1ATvZGniAIIDHAFjyJFpA4z6PKocIKFZbWQ1tw8tRkH9Ta6BWuhUsHwsSFpgd+s
 CojqKYcAV4+xDgb+ZE2JJJJ0ma8QkJi4JKymGJCVljG+a3myQ+3OyN12++AQk80q
 ldCOcdpTuFrXatsvv+ECvOjIA445Hlinfosa7zpXKw4DtUZx3lZYf+oYtTIWcMYb
 78iolJ8qzZVJJUpq94qM+Dd/e057cvEj9CeiBMoXK2VOXOxj/BBVXjHYPq0gtv4=
 =5cT+
 -----END PGP SIGNATURE-----
 

Merge pull request #6321 from libgit2/ethomson/ownership

repo: allow administrator to own the configuration";
        [TestMethod]
        public async Task ReadSignedCommit()
        {
            var src = Bucket.Create.FromASCII(signedCommit.Replace("\r", ""));
            bool readGpg = false;

            var verifySrcReader = GitCommitObjectBucket.ForSignature(src.Duplicate());
            using var commitReader = new GitCommitObjectBucket(src, handleSubBucket);

            await commitReader.ReadUntilEofAsync();

            Assert.IsTrue(readGpg);

            async ValueTask handleSubBucket(GitSubBucketType subBucket, Bucket bucket)
            {
                if (subBucket != GitSubBucketType.Signature)
                    await bucket.ReadUntilEofAndCloseAsync();

                var rdx = new Radix64ArmorBucket(bucket);
                using var gpg = new GitSignatureBucket(rdx);

                await gpg.ReadUntilEofAsync();

                var ok1 = await gpg.VerifyAsync(verifySrcReader, null);
                Assert.IsTrue(ok1, "Verify as signature from 'someone'");

                var ok = await gpg.VerifyAsync(verifySrcReader, await GetKey());
                //Assert.IsTrue(ok);
                readGpg = true;
            }
        }

        const string signedTag =
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

        const string publicKeyOfSignedTag =
@"-----BEGIN PGP PUBLIC KEY BLOCK-----

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
=bYqd
-----END PGP PUBLIC KEY BLOCK-----
";

        async ValueTask<GitPublicKey> GetKey()
        {
            var key = Bucket.Create.FromASCII(publicKeyOfSignedTag.Replace("\r", ""));

            var radix = new Radix64ArmorBucket(key);
            var kb = new GitSignatureBucket(radix);

            await kb.ReadUntilEofAndCloseAsync();
            return await kb.ReadKeyAsync();
        }

        [TestMethod]
        public async Task ReadSignedTag()
        {
            var key = await GetKey();

            var src = Bucket.Create.FromASCII(signedTag.Replace("\r", ""));
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

                await gpg.ReadUntilEofAsync();

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
            var key = Bucket.Create.FromASCII(publicKeyOfSignedTag.Replace("\r", ""));

            var radix = new Radix64ArmorBucket(key);
            var kb = new GitSignatureBucket(radix);

            await kb.ReadUntilEofAndCloseAsync();
            var k = await kb.ReadKeyAsync();
        }

        const string sshSignedCommit =
@"tree 3e0b0fa45d77c89db35a39e7a43b055bd0bcd181
author Jason Miller <contact@jasonmiller.nl> 1638312729 +0100
committer Jason Miller <contact@jasonmiller.nl> 1638312729 +0100
gpgsig -----BEGIN SSH SIGNATURE-----
 U1NIU0lHAAAAAQAAAEoAAAAac2stc3NoLWVkMjU1MTlAb3BlbnNzaC5jb20AAAAg/XfTTM
 09PqIP1qLjTggzPQ40jHERrT0zlDwLOA+CJb4AAAAEc3NoOgAAAANnaXQAAAAAAAAABnNo
 YTUxMgAAAGcAAAAac2stc3NoLWVkMjU1MTlAb3BlbnNzaC5jb20AAABA7Buu75hNZ2bymp
 +9mNcCDb8B2s//7Wx32QkpAXGZ+qCYZL4vt9kgOGWmO0Dp8EvQdhBJnsgG6uzTwr8SqsDO
 DgEAAAAG
 -----END SSH SIGNATURE-----

docs: add README
";
        [TestMethod]
        [Ignore]
        public async Task ReadSignedSshCommit()
        {
#if NETFRAMEWORK || true
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                Assert.Inconclusive("Key algorithm not supported on MONO (yet)");
#endif
            var src = Bucket.Create.FromASCII(sshSignedCommit.Replace("\r", ""));
            bool readGpg = false;

            var verifySrcReader = GitCommitObjectBucket.ForSignature(src.Duplicate());
            using var commitReader = new GitCommitObjectBucket(src, handleSubBucket);

            await commitReader.ReadUntilEofAsync();

            Assert.IsTrue(readGpg);


            async ValueTask handleSubBucket(GitSubBucketType subBucket, Bucket bucket)
            {
                if (subBucket != GitSubBucketType.Signature)
                    await bucket.ReadUntilEofAndCloseAsync();

                var rdx = new Radix64ArmorBucket(bucket);
                using var gpg = new GitSignatureBucket(rdx);

                await gpg.ReadUntilEofAsync();

                var ok = await gpg.VerifyAsync(verifySrcReader, await GetKey());
                //Assert.IsTrue(ok);
                readGpg = true;
            }
        }

        const string dsaKey =
@"-----BEGIN PGP PUBLIC KEY BLOCK-----

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
qH6BKotaAsaFaOvvazluYi9BSNS8
=ez2d
-----END PGP PUBLIC KEY BLOCK-----
";

        async ValueTask<GitPublicKey> GetDSAKey()
        {
            var key = Bucket.Create.FromASCII(dsaKey);

            var radix = new Radix64ArmorBucket(key);
            var kb = new GitSignatureBucket(radix);

            await kb.ReadUntilEofAndCloseAsync();
            return await kb.ReadKeyAsync();
        }

        [TestMethod]
        public async Task VerifyDSA()
        {
            var src = Bucket.Create.FromASCII("test");

            var rdx = new Radix64ArmorBucket(Bucket.Create.FromASCII(sigDSA));
            using var gpg = new GitSignatureBucket(rdx);

            //await gpg.ReadAsync();

            var ok = await gpg.VerifyAsync(src, await GetDSAKey());
            Assert.IsTrue(ok, "DSA ok");
        }

        [TestMethod]
        public async Task VerifySSHRSa()
        {
            var src = Bucket.Create.FromASCII("test");

            var rdx = new Radix64ArmorBucket(Bucket.Create.FromASCII(sshSig));
            using var gpg = new GitSignatureBucket(rdx);

            //await gpg.ReadAsync();

            Assert.IsTrue(GitPublicKey.TryParse("ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQD+aILyycyvtatnMCeyucurSCkXPcuML/bgJOHFCwjeFA8CAXBlOEIgMblj7II8SpKP6mkDEFgXQXAFF6i00UAu8/NBEcW1xdLh0/12X2wlbZIPacMbGI/JbaYD5kznyDhsdyMmMfpbiZIOXPomoVbqcXh8wRqENqu1OTL5zIQBJBflEeZVG4X6oRDquklhrAcna9yPzgqwGHAQTtXpV4n//6bVzus9mTskCNFi+cKvmqifFVAwiG328VkGIrKj/EkB4MB37mIZKYn6Od+0vvzwH+1B5iW66Tjo/A2fx65spqolPLhFR2bmRSxP4Lxsg4R+TBmMDMsDPgeJfD6Iu0efs1s2rqL9VGbVC0Fgw4ds9qCfv2rLO2a+9/U5oopXbgUBlRDXiiX/zer3AFuyieWI3jevrFbRQqhlm08aWM/CP3wrpYikemXydHuxv5YUwJRaCmedOIsz5tVftfD3E+cEOIoLUTkoZwt9Ygywo+pccbTjRFouOcCT1Ib0g+6zgE0= me@pc", out var pk));

            var ok = await gpg.VerifyAsync(src, pk);
            Assert.IsTrue(ok, "RSA ok");
        }

        [TestMethod]
        [DataRow("rsa")]
        [DataRow("dsa")]
        //[DataRow("ecdsa")]
        //[DataRow("ed25519")]
        [Timeout(5000)]
        public async Task TaskVerifyGenerateSSH(string type)
        {
            var dir = TestContext.PerTestDirectory(type);

            string keyFile = Path.Combine(dir, "key");
            RunSshKeyGen("-f", keyFile, "-t", type, "-N", "");

            string publicKey = File.ReadAllText(keyFile + ".pub").Trim();
            Console.WriteLine(publicKey);

            Assert.IsTrue(GitPublicKey.TryParse(publicKey, out var k));

            string testData = Guid.NewGuid().ToString();
            string testDataFile = Path.Combine(dir, "testdata");
            File.WriteAllText(testDataFile, testData);

            RunSshKeyGen("-Y", "sign", "-n", "ns", "-f", keyFile, "-i", testDataFile);

            string signature = File.ReadAllText(testDataFile + ".sig");

            var src = Bucket.Create.FromASCII(testData);
            var rdx = new Radix64ArmorBucket(Bucket.Create.FromASCII(signature));
            using var gpg = new GitSignatureBucket(rdx);

            var ok = await gpg.VerifyAsync(src, k);
        }

        private void RunSshKeyGen(params string[] args)
        {
            FixConsoleEncoding();
            ProcessStartInfo psi = new ProcessStartInfo("ssh-keygen", string.Join(" ", args.Select(x => EscapeGitCommandlineArgument(x.Replace('\\', '/')))));
            Console.WriteLine(psi.Arguments);
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

        private void FixConsoleEncoding()
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
