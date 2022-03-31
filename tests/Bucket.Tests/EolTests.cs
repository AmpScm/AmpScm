using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public TestContext TestContext { get; set; }

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
                                for (int n2 = n1 + 1; n2 < tst.Length-1; n2++)
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

                                        var s= bb.ToASCIIString();
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
            return v.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\0", "\\0");
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
                                        var (bb, eol) = await r.ReadUntilEolAsync(acceptableEols, n2-n1);

                                        Assert.IsTrue(bb.Length <= (n2 - n1), "Did not read more than requested");
                                    }
                                }
                        }
        }


        private Bucket MakeBucket(params string[] args)
        {
            return new AggregateBucket(args.Select(x => Encoding.ASCII.GetBytes(x).AsBucket()).ToArray());
        }

    }
}
