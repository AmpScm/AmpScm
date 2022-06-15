using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Git.Objects;
using AmpScm.Buckets.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests.Buckets
{
    [TestClass]
    public class SignatureTests
    {
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

        const string sshSig =
@"-----BEGIN SSH SIGNATURE-----
U1NIU0lHAAAAAQAAAEoAAAAac2stc3NoLWVkMjU1MTlAb3BlbnNzaC5jb20AAAAg/XfTTM
09PqIP1qLjTggzPQ40jHERrT0zlDwLOA+CJb4AAAAEc3NoOgAAAANnaXQAAAAAAAAABnNo
YTUxMgAAAGcAAAAac2stc3NoLWVkMjU1MTlAb3BlbnNzaC5jb20AAABA7Buu75hNZ2bymp
+9mNcCDb8B2s//7Wx32QkpAXGZ+qCYZL4vt9kgOGWmO0Dp8EvQdhBJnsgG6uzTwr8SqsDO
DgEAAAAG
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
        [DataRow(sshSig, DisplayName = nameof(sshSig))]
        //[DataRow(sshSig2, DisplayName = nameof(sshSig2))]
        public async Task ParseRfc4880(string signature)
        {
            var b = Bucket.Create.FromASCII(signature + Environment.NewLine + "TAIL!");

            var sr = new Radix64ArmorBucket(b);
            using var rr = new OpenPgpSignatureBucket(sr);

            var bb = await rr.ReadExactlyAsync(8192);

            Assert.AreEqual(OpenPgpTagType.Signature, await rr.ReadTagAsync());

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
            var src = Bucket.Create.FromASCII(mergetag.Replace("\r",""));
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

            using var commitReader = new GitCommitObjectBucket(src, handleSubBucket);


            await commitReader.ReadUntilEofAsync();

            Assert.IsTrue(readGpg);


            async ValueTask handleSubBucket(GitSubBucketType subBucket, Bucket bucket)
            {
                if (subBucket != GitSubBucketType.Signature)
                    await bucket.ReadUntilEofAndCloseAsync();

                var rdx = new Radix64ArmorBucket(bucket);
                using var gpg = new OpenPgpSignatureBucket(rdx);

                await gpg.ReadUntilEofAsync();
                readGpg = true;
            }
        }
    }
}
