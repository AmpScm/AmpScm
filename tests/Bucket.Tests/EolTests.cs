using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using AmpScm.BucketTests.Buckets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BucketTests
{
    [TestClass]
    public class EolTests
    {
        public TestContext TestContext { get; set; } = default!;

        [TestMethod]
        public async Task ReadEols()
        {
            var (bb, eol) = await MakeBucket("abc\nabc").ReadUntilEolAsync(BucketEol.LF);

            Assert.AreEqual(4, bb.Length);
            Assert.AreEqual("abc\n", bb.ToASCIIString());
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.LF, eol);

            var r = MakeBucket("abc\0abc");
            (bb, eol) = await r.ReadUntilEolAsync(BucketEol.Zero);

            Assert.AreEqual(4, bb.Length);
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.Zero, eol);

            (bb, eol) = await r.ReadUntilEolAsync(BucketEol.Zero);

            Assert.AreEqual(3, bb.Length);
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.None, eol);

            (bb, eol) = await r.ReadUntilEolAsync(BucketEol.Zero);

            Assert.AreEqual(0, bb.Length);
            Assert.IsTrue(bb.IsEof);
            Assert.AreEqual(BucketEol.None, eol);

            r = MakeBucket("a", "b", "c", "\0a", "bc", "d\0a", "b", "c", "\0", "a");
            string total = "";
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolAsync(BucketEol.Zero);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString();

                if (eol != BucketEol.None)
                    total += "!";
            }

            Assert.AreEqual("abc\0abcd\0abc\0a", total.Replace("|", "").Replace("!", ""));
            Assert.AreEqual("|a|bc|\0!|a|bc|d\0!|a|bc|\0!|a", total);

            r = MakeBucket("a", "b", "c", "\0");
            total = "";
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolAsync(BucketEol.Zero);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString();

                if (eol != BucketEol.None)
                    total += "!";
            }

            Assert.AreEqual("abc\0", total.Replace("|", "").Replace("!", ""));
            Assert.AreEqual("|a|bc|\0!", total);


            r = MakeBucket("a\r\nb\rcd\r", "\nefg\rhi\r\n", "j\r", "\rk");

            total = "";
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolAsync(BucketEol.AnyEol);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }

            Assert.AreEqual("|a[CRLF]|b[CR]|cd[CRSplit]|[LF]|efg[CR]|hi[CRLF]|j[CRSplit]|[CR]|k[None]",
                            total.Replace("\r", "/r/"));

            r = MakeBucket("a\r");
            total = "";
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolAsync(BucketEol.AnyEol);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }

            Assert.AreEqual("|a[CRSplit]",
                            total.Replace("\r", "/r/"));


            r = MakeBucket("H", "T", "T", "P", "/", "1", ".", "1", "\r", "\n", "a");

            total = "";
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolAsync(BucketEol.AnyEol);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }

            Assert.AreEqual("|H[None]|TT[None]|P/[None]|1.[None]|1[CRSplit]|[LF]|a[None]",
                            total.Replace("\r", "/r/"));


            r = MakeBucket("H", "T", "T", "P", "/", "1", ".", "1", "\r", "\n\n\r", "\0a");

            total = "";
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolAsync(BucketEol.AnyEol);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }

            Assert.AreEqual("|H[None]|TT[None]|P/[None]|1.[None]|1[CRSplit]|[LF]|[LF]|[CRSplit]|\0a[None]",
                            total.Replace("\r", "/r/"));
        }

        [TestMethod]
        public async Task ReadEolsFull()
        {
            var (bb, eol) = await MakeBucket("abc\nabc").ReadUntilEolFullAsync(BucketEol.LF, new BucketEolState());

            Assert.AreEqual(4, bb.Length);
            Assert.AreEqual("abc\n", bb.ToASCIIString());
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.LF, eol);

            (bb, eol) = await MakeBucket("abc\0abc").ReadUntilEolFullAsync(BucketEol.Zero, new BucketEolState());

            Assert.AreEqual(4, bb.Length);
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.Zero, eol);

            var b = MakeBucket("a", "b", "c", "\0a", "bc", "d\0a", "b", "c", "\0", "a");

            (bb, eol) = await b.ReadUntilEolFullAsync(BucketEol.Zero, new BucketEolState());

            Assert.AreEqual(4, bb.Length);
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.Zero, eol);

            (bb, eol) = await b.ReadUntilEolFullAsync(BucketEol.Zero, new BucketEolState());
            Assert.AreEqual(5, bb.Length);
            Assert.AreEqual("abcd", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.Zero, eol);

            (bb, eol) = await b.ReadUntilEolFullAsync(BucketEol.Zero, new BucketEolState());

            Assert.AreEqual(4, bb.Length);
            Assert.AreEqual("abc", bb.ToASCIIString(eol));
            Assert.AreEqual(BucketEol.Zero, eol);

            var r = MakeBucket("a", "b", "c", "\0");
            var total = "";
            var state = new BucketEolState();
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.Zero, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString();

                if (eol != BucketEol.None)
                    total += "!";
            }

            Assert.AreEqual("abc\0", total.Replace("|", "").Replace("!", ""));
            Assert.AreEqual("|abc\0!", total);

            r = MakeBucket("a\r\nb\rcd\r", "\nefg\rhi\r\n", "j\r", "\rk");

            total = "";
            state = new BucketEolState();
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.AnyEol, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }
            Assert.AreEqual("|a[CRLF]|b[CR]|cd[CRLF]|efg[CR]|hi[CRLF]|j[CR]|[CR]|k[None]",
                            total.Replace("\r", "/r/"));

            r = MakeBucket("a\r");
            total = "";
            state = new BucketEolState();
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.AnyEol, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }

            Assert.AreEqual("|a[CR]",
                            total.Replace("\r", "/r/"));


            r = MakeBucket("H", "T", "T", "P", "/", "1", ".", "1", "\r", "\n", "a");

            total = "";
            state = new BucketEolState();
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.AnyEol, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }

            Assert.AreEqual("|HTTP/1.1[CRLF]|a[None]",
                            total.Replace("\r", "/r/"));


            r = MakeBucket("H", "T", "T", "P", "/", "1", ".", "1", "\r", "\n\n\r", "\0a\r", "\r\nc", "\r", "\r\n", "qq\r", "q\r", "\r", "\n");

            total = "";
            state = new BucketEolState();
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.AnyEol, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }

            Assert.AreEqual("|HTTP/1.1[CRLF]|[LF]|[CR]|\0a[CR]|[CRLF]|c[CR]|[CRLF]|qq[CR]|q[CR]|[CRLF]",
                            total.Replace("\r", "/r/"));


            r = MakeBucket("H", "T", "T", "P", "/", "1", ".", "1", "\r", "\n\n\r", "\0a\r", "\r\nc", "\r", "\r\n", "qq\r", "q\r", "\r\0\r", "\0");

            total = "";
            state = new BucketEolState();
            while (true)
            {
                (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.AnyEol | BucketEol.Zero, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }
            Assert.AreEqual("|HTTP/1.1[CRLF]|[LF]|[CR]|[Zero]|a[CR]|[CRLF]|c[CR]|[CRLF]|qq[CR]|q[CR]|[CR]|[Zero]|[CR]|[Zero]",
                            total.Replace("\r", "/r/"));
        }

        [TestMethod]
        public async Task EolCrCrCrLf()
        {
            {
                var (bb, eol) = await MakeBucket("\r\n").ReadUntilEolAsync(BucketEol.AnyEol, 1);

                Assert.AreEqual(1, bb.Length);
                Assert.AreEqual(BucketEol.CRSplit, eol);
            }

            var r = MakeBucket("a", "t", "\r\r\r\ndo");
            var total = "";
            var state = new BucketEolState();
            while (true)
            {
                var (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.AnyEol, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }
            Assert.AreEqual("|at[CR]|[CR]|[CRLF]|do[None]",
                            total.Replace("\r", "/r/"));
        }

        [TestMethod]
        public async Task EolCrCrZeroCr()
        {
            var r = MakeBucket("a", "t", "\r\r\0\rdo");
            var total = "";
            var state = new BucketEolState();
            while (true)
            {
                var (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.AnyEol, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }
            Assert.AreEqual("|at[CR]|[CR]|\0[CR]|do[None]",
                            total.Replace("\r", "/r/"));
        }

        [TestMethod]
        public async Task EolCrCrCrZero()
        {
            var r = MakeBucket("a", "t\r\r\r", "\0do");
            var total = "";
            var state = new BucketEolState();
            while (true)
            {
                var (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.CRLF | BucketEol.Zero, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }
            Assert.AreEqual("|at/r//r//r/[Zero]|do[None]",
                            total.Replace("\r", "/r/"));
        }

        [TestMethod]
        public async Task EolCrCrCrCr()
        {
            var r = MakeBucket("\r\r\r\rdo");
            var total = "";
            var state = new BucketEolState();
            while (true)
            {
                var (bb, eol) = await r.ReadUntilEolFullAsync(BucketEol.CR, state);

                if (bb.IsEof)
                    break;

                total += "|" + bb.ToASCIIString(eol) + $"[{eol}]";
            }
            Assert.AreEqual("|[CR]|[CR]|[CR]|[CR]|do[None]",
                            total.Replace("\r", "/r/"));
        }


        public static IEnumerable<object[]> EolFlags => Enumerable.Range(1, (int)(BucketEol.AnyEol | BucketEol.Zero)).Select(i => new object[] { (BucketEol)i });
        [TestMethod]
        [DynamicData(nameof(EolFlags))]
        public async Task EolVariantsFull(BucketEol acceptableEols)
        {
            var opts = new[] { "\r", "\n", "\r\n", "\0", " " };
            foreach (var c1 in opts)
                foreach (var c2 in opts)
                    foreach (var c3 in opts)
                        foreach (var c4 in opts)
                        {
                            string tst = $"st{c1}{c2}{c3}{c4}do";

                            for (int n1 = 1; n1 < tst.Length - 2; n1++)
                                for (int n2 = n1 + 1; n2 < tst.Length - 1; n2++)
                                {
                                    using var r = MakeBucket(tst.Substring(0, n1), tst.Substring(n1, n2 - n1), tst.Substring(n2));
                                    var state = new BucketEolState();
                                    var total = "";
                                    var expected = Escape(tst.Insert(n2, "|").Insert(n1, "|"));
                                    while (true)
                                    {
                                        var (bb, eol) = await r.ReadUntilEolFullAsync(acceptableEols, state);

                                        if (bb.IsEof)
                                            break;

                                        var s = bb.ToASCIIString();
                                        total += s;

                                        if (s.Length > 2 && 0 != (acceptableEols & BucketEol.LF) && s.LastIndexOf('\n', s.Length - 2) >= 0)
                                            Assert.Fail($"Unexpected LF in test {expected}, result={Escape(total)}");
                                        if (s.Length > 2 && 0 != (acceptableEols & BucketEol.Zero) && s.LastIndexOf('\0', s.Length - 2) >= 0)
                                            Assert.Fail($"Unexpected Zero in test {expected}, part={Escape(s)}");
                                        if (s.Length > 3 && 0 != (acceptableEols & BucketEol.CR) && s.LastIndexOf('\r', s.Length - 3) >= 0)
                                            Assert.Fail($"Unexpected CR in test {expected}, part={Escape(s)}");
                                    }

                                    Assert.AreEqual(expected, Escape(total.Insert(n2, "|").Insert(n1, "|")));
                                }
                        }
        }

        string Escape(string v)
        {
            StringBuilder sb = new StringBuilder(v.Length);

            foreach (char c in v)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\0':
                        sb.Append("\\0");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(c) || char.IsWhiteSpace(c) || c >= 0x80)
                        {
                            sb.Append((string)$"\\x{(int)c:x2}");
                        }
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        [TestMethod]
        [DynamicData(nameof(EolFlags))]
        public async Task EolVariants(BucketEol acceptableEols)
        {
            var opts = new[] { "\r", "\n", "\r\n", "\0", " " };
            foreach (var c1 in opts)
                foreach (var c2 in opts)
                    foreach (var c3 in opts)
                        foreach (var c4 in opts)
                        {
                            string tst = $"st{c1}{c2}{c3}{c4}do";

                            for (int n1 = 1; n1 < tst.Length - 2; n1++)
                                for (int n2 = n1 + 1; n2 < tst.Length - 1; n2++)
                                {
                                    using var r = MakeBucket(tst.Substring(0, n1), tst.Substring(n1, n2 - n1), tst.Substring(n2));
                                    var total = "";

                                    while (true)
                                    {
                                        var (bb, eol) = await r.ReadUntilEolAsync(acceptableEols);

                                        if (bb.IsEof)
                                            break;

                                        total += bb.ToASCIIString();
                                    }

                                    Assert.AreEqual(Escape(tst.Insert(n2, "|").Insert(n1, "|")), Escape(total.Insert(n2, "|").Insert(n1, "|")));

                                    using (var r2 = MakeBucket(tst.Substring(0, n1)))
                                    {
                                        var (bb, eol) = await r.ReadUntilEolAsync(acceptableEols, n2 - n1);

                                        Assert.IsTrue(bb.Length <= (n2 - n1), "Did not read more than requested");
                                    }
                                }
                        }
        }


        [TestMethod]
        public void EolNormalizeSpecific()
        {
            string Apply(string tst, BucketEol acceptedEols = BucketEol.AnyEol)
            {
                var r = MakeBucket(tst).NormalizeEols(acceptedEols);
                var bb = r.ReadFullAsync(256).AsTask().Result;

                return bb.ToUTF8String();
            }

            string Apply2(string[] tst, BucketEol acceptedEols = BucketEol.AnyEol)
            {
                var r = MakeBucket(tst).NormalizeEols(acceptedEols);
                var bb = r.ReadFullAsync(256).AsTask().Result;

                return bb.ToUTF8String();
            }

            string tst = "aap\r\r\r\r\r";
            Assert.AreEqual(Escape(tst.Replace('\r', '\n')), Escape(Apply(tst)));

            tst = "aap\r\\r\n\r\r";
            Assert.AreEqual(Escape(tst.Replace("\r\n", "\n").Replace('\r', '\n')), Escape(Apply(tst)));


            tst = "aap\r\r\r\n";
            var tst2 = new[] { "aap\r\r", "\r\n" };
            Assert.AreEqual(Escape(tst.Replace("\r\n", "\n")), Escape(Apply2(tst2, BucketEol.CRLF)));


            tst = "st\r\r\r\0do";
            tst2 = new[] { "s", "t", "\r\r\r\0do" };
            Assert.AreEqual(Escape(tst.Replace("\0", "\n")), Escape(Apply2(tst2, BucketEol.CRLF | BucketEol.Zero)));
        }

        [TestMethod]
        [DynamicData(nameof(EolFlags))]
        public async Task EolNormalize(BucketEol acceptableEols)
        {
            var opts = new[] { "\r", "\n", "\r\n", "\0", " " };
            foreach (var c1 in opts)
                foreach (var c2 in opts)
                    foreach (var c3 in opts)
                        foreach (var c4 in opts)
                        {
                            string tst = $"st{c1}{c2}{c3}{c4}do";

                            for (int n1 = 1; n1 < tst.Length - 2; n1++)
                                for (int n2 = n1 + 1; n2 < tst.Length - 1; n2++)
                                {
                                    using var r = MakeBucket(tst.Substring(0, n1), tst.Substring(n1, n2 - n1), tst.Substring(n2)).NormalizeEols(acceptableEols);
                                    var total = "";

                                    while (true)
                                    {
                                        var bb = await r.ReadAsync();

                                        if (bb.IsEof)
                                            break;

                                        total += bb.ToASCIIString();
                                    }

                                    var exp = tst;

                                    if ((acceptableEols & BucketEol.CRLF) != 0)
                                        exp = exp.Replace("\r\n", "\n");
                                    if ((acceptableEols & BucketEol.CR) != 0)
                                        exp = exp.Replace("\r", "\n");
                                    if ((acceptableEols & BucketEol.Zero) != 0)
                                        exp = exp.Replace("\0", "\n");
                                    Assert.AreEqual(Escape(exp), Escape(total), $"In case {Escape(c1)}, {Escape(c2)}, {Escape(c3)}, {Escape(c4)}, n1={n1}, n2={n2}");

                                    using (var r2 = MakeBucket(tst.Substring(0, n1)))
                                    {
                                        var (bb, eol) = await r.ReadUntilEolAsync(acceptableEols, n2 - n1);

                                        Assert.IsTrue(bb.Length <= (n2 - n1), "Did not read more than requested");
                                    }
                                }
                        }
        }

        public static IEnumerable<object[]> EncodingList { get; } = new Encoding[] { new UTF8Encoding(true), new UTF8Encoding(false), ANSI, new UnicodeEncoding(false, true), new UnicodeEncoding(true, true), new UnicodeEncoding(false, false), new UnicodeEncoding(true, false) }.Select(x => new object[] { x });

        [TestMethod]
        [DynamicData(nameof(EncodingList), DynamicDataDisplayName = nameof(EncodingDisplayName))]
        public async Task NormalizeBucketToUtf8(Encoding enc)
        {
            var data = Enumerable.Range(0, byte.MaxValue).Select(x => ANSI.GetChars(new byte[] { (byte)x })[0]).ToArray();
            var encodedBytes = (enc.GetPreamble().ToArray().AsBucket() + enc.GetBytes(data).AsBucket()).ToArray();
            using var b = encodedBytes.AsBucket();

            // This wil test the peaking
            var bb = await b.NormalizeToUtf8().ReadFullAsync(1024);
            Assert.AreEqual(Escape(new String(data)), Escape(bb.ToUTF8String()), Escape(new String(encodedBytes.Select(x => (char)x).ToArray())));

            if (enc is UnicodeEncoding u && !u.GetPreamble().Any())
                return; // Unicode without preamble not detectable without scan via .Peek()

            // This will check the byte reading
            BucketBytes ec = encodedBytes;
            bb = await (ec.Slice(0, 1).ToArray().AsBucket() + ec.Slice(1).ToArray().AsBucket()).NormalizeToUtf8().ReadFullAsync(1024);
            Assert.AreEqual(Escape(new String(data)), Escape(bb.ToUTF8String()), Escape(new String(encodedBytes.Select(x => (char)x).ToArray())));
        }

        [TestMethod]
        [DynamicData(nameof(EncodingList), DynamicDataDisplayName = nameof(EncodingDisplayName))]
        public async Task ConvertBucketToUtf8(Encoding enc)
        {
            var data = Enumerable.Range(0, byte.MaxValue).Select(x => ANSI.GetChars(new byte[] { (byte)x })[0]).ToArray();
            var encodedBytes = (enc.GetPreamble().ToArray().AsBucket() + enc.GetBytes(data).AsBucket()).ToArray();
            using var b = encodedBytes.AsBucket();

            var bb = await b.ConvertToUtf8(enc).ReadFullAsync(1024);
            Assert.AreEqual(Escape(new String(data)), Escape(bb.ToUTF8String()), Escape(new String(encodedBytes.Select(x => (char)x).ToArray())));
        }

        [TestMethod]
        [DynamicData(nameof(EncodingList), DynamicDataDisplayName = nameof(EncodingDisplayName))]

        public async Task BomScan(Encoding enc)
        {
            var data = Enumerable.Range(0, byte.MaxValue).Select(x => ANSI.GetChars(new byte[] { (byte)x })[0]).ToArray();
            var encodedBytes = (enc.GetPreamble().ToArray().AsBucket() + enc.GetBytes(data).AsBucket()).ToArray();

            for(int i = 1; i < encodedBytes.Length-1;i++)
            {
                var encSpan = encodedBytes.AsMemory();

                var b = encSpan.Slice(0, i).ToArray().AsBucket() + encSpan.Slice(i).ToArray().AsBucket();

                BucketBytes bb = await b.ConvertToUtf8(enc).ReadFullAsync(1024);
                Assert.AreEqual(Escape(new String(data)), Escape(bb.ToUTF8String()), $"Convert Iteration {i}");

                if (enc is UnicodeEncoding u && !u.GetPreamble().Any())
                    continue; // Unicode without preamble not detectable without scan via .Peek()

                b = encSpan.Slice(0, i).ToArray().AsBucket() + encSpan.Slice(i).ToArray().AsBucket();
                bb = await b.NormalizeToUtf8().ReadFullAsync(1024);
                Assert.AreEqual(Escape(new String(data)), Escape(bb.ToUTF8String()), $"Normalize Iteration {i}");
            }
        }

        [TestMethod]
        public async Task EncodeScan()
        {
            var enc = new UTF8Encoding(false);
            var data = Enumerable.Range(120, 20).Select(x => ANSI.GetChars(new byte[] { (byte)x })[0]).ToArray();
            var encodedBytes = (enc.GetPreamble().ToArray().AsBucket() + enc.GetBytes(data).AsBucket()).ToArray();

            for (int i = 1; i < encodedBytes.Length - 1; i++)
            {
                var encSpan = encodedBytes.AsMemory();

                var b = encSpan.Slice(0, i).ToArray().AsBucket() + encSpan.Slice(i).ToArray().AsBucket();

                BucketBytes bb = await b.ConvertToUtf8(enc).ReadFullAsync(1024);
                Assert.AreEqual(Escape(new String(data)), Escape(bb.ToUTF8String()), $"Convert Iteration {i}");

                b = encSpan.Slice(0, i).ToArray().AsBucket() + encSpan.Slice(i).ToArray().AsBucket();
                bb = await b.NormalizeToUtf8().ReadFullAsync(1024);
                Assert.AreEqual(Escape(new String(data)), Escape(bb.ToUTF8String()), $"Normalize Iteration {i}");
            }
        }

        private static Bucket MakeBucket(params string[] args)
        {
            return new AggregateBucket(args.Select(x => Encoding.ASCII.GetBytes(x).AsBucket()).ToArray());
        }

        static Encoding ANSI => (Encoding.Default is UTF8Encoding)
#if NET5_0_OR_GREATER
            ? Encoding.Latin1
#else
            ? Encoding.GetEncoding("ISO-8859-1")
#endif
            : Encoding.Default;

        public static string EncodingDisplayName(MethodInfo mi, object[] args)
        {
            var enc = (Encoding)args.Last();

            return $"{mi.Name}({enc.EncodingName}, bom: {enc.GetPreamble().Any()})";
        }
    }
}
