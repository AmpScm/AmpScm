using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests.Buckets
{
    [TestClass]
    public class SignatureTests
    {
        static readonly string sig1 =
@"-----BEGIN PGP SIGNATURE-----

wsBcBAABCAAQBQJioEepCRBK7hj4Ov3rIwAAGoMIAHCHbTas3gShVMMX2dx2r82B
33bY2C8sQ+jFyrJHid8Kq8CMokk2cgPNfELyUw/Sjce4M/CxWq6didx58OOg6nom
XIPqvRsHqFuNpuC0Ku9vW4fuiITD6i8ADPpNwsU2lVVSqwVdmdBCU5PncXyYk3Bs
0P1rgF80R9NepCqZ0FihrscJMqX72F5xiq+EGAa/fz4QWDpi792B1fOOO3Q412R6
KJWsEX/HpigGCiiQ/BHf/z3hj9URNEKOWd2hRAJsvkYzczhV8/yZmSjlg9kUp/Cw
aWhgukgOUppFsmnAfSp4zz0MmV2vbAKJQrrTmi1PmDFXt/mDv5xCifZpWbS46cY=
=7fmL
-----END PGP SIGNATURE-----";

        [TestMethod]
        public async Task ParseSig1()
        {
            var b = Bucket.Create.FromASCII(sig1);

            using var sr = new OpenPgpArmorBucket(b);

            while(true)
            {
                var bb = await sr.ReadHeaderAsync();
                if (bb.IsEof)
                    break;
            }

            var dt = await sr.ReadExactlyAsync(Bucket.MaxRead);
        }

        [TestMethod]
        public async Task ParseSigTail()
        {
            var b = Bucket.Create.FromASCII(sig1 + Environment.NewLine + "TAIL!");

            using var sr = new OpenPgpArmorBucket(b);

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
    }
}
