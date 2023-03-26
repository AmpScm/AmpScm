﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Cryptography;
using AmpScm.Buckets.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BucketTests.Security
{
    [TestClass]
    public class OCBTests
    {
        static readonly byte[] Key = Dec("000102030405060708090A0B0C0D0E0F");

        internal static void InconclusiveOnMono()
        {
#if NETFRAMEWORK
            //if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            //    Assert.Inconclusive("OCB/AES doesn't work nicely on mono. Please use .Net Core");
#endif
        }

        [TestMethod]
        public async Task Ocb_00_Empty()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("785407BFFFC8AD9EDCC5520AC9111EE6").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA99887766554433221100"),
                associatedData: default,
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_01_Basic()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("6820B3657B6F615A5725BDA0D3B4EB3A257C9AF1F8F03009").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA99887766554433221101"),
                associatedData: Dec("0001020304050607"),
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("0001020304050607", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_02_Associated()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("81017F8203F081277152FADE694A0A00").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA99887766554433221102"),
                associatedData: Dec("0001020304050607"),
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_03_NoAssociated()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("45DD69F8F5AAE72414054CD1F35D82760B2CD00D2F99BFA9").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA99887766554433221103"),
                associatedData: default,
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("0001020304050607", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_04_Longer()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("571D535B60B277188BE5147170A9A22C3AD7A4FF3835B8C5701C1CCEC8FC3358").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA99887766554433221104"),
                associatedData: Dec("000102030405060708090A0B0C0D0E0F"),
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("000102030405060708090A0B0C0D0E0F", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_05_EmptyLonger()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("8CF761B6902EF764462AD86498CA6B97").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA99887766554433221105"),
                associatedData: Dec("000102030405060708090A0B0C0D0E0F"),
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_06_NoAssociatedLonger()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("5CE88EC2E0692706A915C00AEB8B2396F40E1C743F52436BDF06D8FA1ECA343D").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA99887766554433221106"),
                associatedData: default,
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("000102030405060708090A0B0C0D0E0F", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_07_Longer()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("1CA2207308C87C010756104D8840CE1952F09673A448A122C92C62241051F57356D7F3C90BB0E07F").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA99887766554433221107"),
                associatedData: Dec("000102030405060708090A0B0C0D0E0F1011121314151617"),
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("000102030405060708090A0B0C0D0E0F1011121314151617", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_08_LongerEmpty()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("6DC225A071FC1B9F7C69F93B0F1E10DE").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA99887766554433221108"),
                associatedData: Dec("000102030405060708090A0B0C0D0E0F1011121314151617"),
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_09_LongerNoAssociated()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("221BD0DE7FA6FE993ECCD769460A0AF2D6CDED0C395B1C3CE725F32494B9F914D85C0B1EB38357FF").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA99887766554433221109"),
                associatedData: default,
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("000102030405060708090A0B0C0D0E0F1011121314151617", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_10_Longer()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("BD6F6C496201C69296C11EFD138A467ABD3C707924B964DEAFFC40319AF5A48540FBBA186C5553C68AD9F592A79A4240").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA9988776655443322110A"),
                associatedData: Dec("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F"),
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_11_LongerEmpty()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("FE80690BEE8A485D11F32965BC9D2A32").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA9988776655443322110B"),
                associatedData: Dec("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F"),
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_12_LongerNoAssociated()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("2942BFC773BDA23CABC6ACFD9BFD5835BD300F0973792EF46040C53F1432BCDFB5E1DDE3BC18A5F840B52E653444D5DF").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA9988776655443322110C"),
                associatedData: default,
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_13_Longer()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("D5CA91748410C1751FF8A2F618255B68A0A12E093FF454606E59F9C1D0DDC54B65E8628E568BAD7AED07BA06A4A69483A7035490C5769E60").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA9988776655443322110D"),
                associatedData: Dec("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F2021222324252627"),
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F2021222324252627", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_14_LongerEmpty()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("C5CD9D1850C141E358649994EE701B68").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA9988776655443322110E"),
                associatedData: Dec("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F2021222324252627"),
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }

        [TestMethod]
        public async Task Ocb_15_LongerNoAssociated()
        {
            InconclusiveOnMono();

            bool? ok = null;

            var b = new OcbDecodeBucket(
                inner: Dec("4412923493C57D5DE0D700F753CCE0D1D2D95060122E9F15A5DDBFC5787E50B5CC55EE507BCB084E479AD363AC366B95A98CA5F3000B1479").AsBucket(),
                Key, 128,
                nonce: Dec("BBAA9988776655443322110F"),
                associatedData: default,
                verifyResult: r => ok = r);


            var r = await b.ReadExactlyAsync(1024);
            Assert.AreEqual("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F2021222324252627", GetHex(r));
            Assert.AreEqual(true, ok, "Got verified");
        }


        static byte[] Dec(string txt)
        {
            byte[] result = new byte[txt.Length / 2];

            for(int i = 0; i < txt.Length; i+= 2)
            {
                result[i / 2] = (byte)int.Parse(txt.Substring(i, 2), System.Globalization.NumberStyles.HexNumber);
            }

            return result;
        }

        static string GetHex(BucketBytes bb)
        {
            StringBuilder sb = new StringBuilder();

            for(int i = 0; i < bb.Length; i++)
            {
                sb.Append(bb[i].ToString("X2"));
            }

            return sb.ToString();
        }

    }
}
