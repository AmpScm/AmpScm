using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AmpScm;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using AmpScm.BucketTests.Buckets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BucketTests
{
    [TestClass]
    public class BasicTests
    {

        [TestMethod]
        public async Task BasicReadMemory()
        {
            byte[] buffer = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();

            using var mb = new MemoryBucket(buffer);

            BucketBytes bb;

            bb = mb.Peek();
            Assert.AreEqual(256, bb.Length);

            Assert.AreEqual(256L, await mb.ReadRemainingBytesAsync());

            bb = await mb.ReadAsync(10);
            Assert.AreEqual(10, bb.Length);

            bb = await mb.ReadAsync();
            Assert.AreEqual(246, bb.Length);

            bb = await mb.ReadAsync();
            Assert.IsTrue(bb.IsEof);
        }

        [TestMethod]
        public async Task BasicAggregate()
        {
            byte[] buffer = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();

            var mb1 = new MemoryBucket(buffer);
            var mb = mb1.Append(new MemoryBucket(buffer));

            BucketBytes bb;

            bb = mb.Peek();
            Assert.AreEqual(256, bb.Length);

            Assert.AreEqual(512L, await mb.ReadRemainingBytesAsync());

            bb = await mb.ReadAsync(10);
            Assert.AreEqual(10, bb.Length);

            bb = await mb.ReadAsync();
            Assert.AreEqual(246, bb.Length);

            bb = await mb.ReadAsync();
            Assert.AreEqual(256, bb.Length);

            bb = await mb.ReadAsync();
            Assert.IsTrue(bb.IsEof);
        }

        [TestMethod]
        public async Task BasicHash()
        {
            var strings = new[] { "This is string1\n", "This is string2\n", "This is string 3\n" };
            byte[][] buffers = strings.Select(x => System.Text.Encoding.UTF8.GetBytes(x)).ToArray();

            var b = buffers.AsBucket(true);

            var r = await b.ReadSkipAsync(1024);
            Assert.AreEqual(49L, r);

            await b.ResetAsync();

            var c = new AmpScm.Buckets.Specialized.CreateHashBucket(b, MD5.Create());

            r = await c.ReadSkipAsync(1024);
            Assert.AreEqual(49L, r);

            Assert.IsNotNull(c.HashResult);
            Assert.AreEqual("E358B5530A87E41AF9168B4F45548AFC", TestExtensions.FormatHash(c.HashResult));

            await b.ResetAsync();
            var c2 = new AmpScm.Buckets.Specialized.CreateHashBucket(b, SHA1.Create());

            r = await c2.ReadSkipAsync(1024);
            Assert.AreEqual(49L, r);

            Assert.IsNotNull(c2.HashResult);
            Assert.AreEqual("D9F7CE90FB58072D8A68F69A0CB30C133F9B08CB", TestExtensions.FormatHash(c2.HashResult));

            await c2.ResetAsync();

            r = await c2.ReadSkipAsync(1024);
            Assert.AreEqual(49L, r);

            Assert.IsNotNull(c2.HashResult);
            Assert.AreEqual("D9F7CE90FB58072D8A68F69A0CB30C133F9B08CB", TestExtensions.FormatHash(c2.HashResult));

#if NET5_0_OR_GREATER
            {
                var rr = MD5.HashData(Encoding.ASCII.GetBytes(string.Join("", strings)));

                Assert.AreEqual("E358B5530A87E41AF9168B4F45548AFC", TestExtensions.FormatHash(rr));

                rr = SHA1.HashData(Encoding.ASCII.GetBytes(string.Join("", strings)));

                Assert.AreEqual("D9F7CE90FB58072D8A68F69A0CB30C133F9B08CB", TestExtensions.FormatHash(rr));
            }
#endif
        }

        // Git blob of "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
        static readonly byte[] git_blob_ABCDEFGHIJKLMNOPQRSTUVWXYZ = {
            0x78, 0x01, 0x4b, 0xca, 0xc9, 0x4f, 0x52, 0x30, 0x32, 0x63, 0x70, 0x74,
            0x72, 0x76, 0x71, 0x75, 0x73, 0xf7, 0xf0, 0xf4, 0xf2, 0xf6, 0xf1, 0xf5,
            0xf3, 0x0f, 0x08, 0x0c, 0x0a, 0x0e, 0x09, 0x0d, 0x0b, 0x8f, 0x88, 0x8c,
            0x02, 0x00, 0xa8, 0xae, 0x0a, 0x07
        };

        [TestMethod]
        public async Task BasicDecompress()
        {
            using var b = git_blob_ABCDEFGHIJKLMNOPQRSTUVWXYZ.AsBucket();

            var r = b.Decompress(BucketCompressionAlgorithm.ZLib);


            byte[] chars34 = new byte[34];
            r.ReadFull(chars34);

            Assert.AreEqual("blob 26\0ABCDEFGHIJKLMNOPQRSTUVWXYZ", Encoding.ASCII.GetString(chars34));

            var v = await r.ReadAsync();

            Assert.IsTrue(v.IsEof);

            v = await b.ReadAsync();
            Assert.IsTrue(v.IsEof);

            await b.ResetAsync();
            r = b.PerByte().Decompress(BucketCompressionAlgorithm.ZLib);
            r.ReadFull(chars34);
            Assert.AreEqual("blob 26\0ABCDEFGHIJKLMNOPQRSTUVWXYZ", Encoding.ASCII.GetString(chars34));

            v = await r.ReadAsync();

            Assert.IsTrue(v.IsEof);

            v = await b.ReadAsync();
            Assert.IsTrue(v.IsEof);
        }

        [TestMethod]
        public async Task BasicDecompressTrail()
        {
            var b = git_blob_ABCDEFGHIJKLMNOPQRSTUVWXYZ.AsBucket().Append(new byte[] { 255, 100, 101 }.AsBucket());
            {
                var r = b.Decompress(BucketCompressionAlgorithm.ZLib);


                byte[] chars34 = new byte[34];
                r.ReadFull(chars34);

                Assert.AreEqual("blob 26\0ABCDEFGHIJKLMNOPQRSTUVWXYZ", Encoding.ASCII.GetString(chars34));

                var v = await r.ReadAsync();

                v = await b.ReadAsync();
                Assert.IsFalse(v.IsEof);
                Assert.AreEqual(3, v.Length);
            }

            b = git_blob_ABCDEFGHIJKLMNOPQRSTUVWXYZ.AsBucket().Append(new byte[] { 255, 100, 101 }.AsBucket()).ToArray().AsBucket();

            {
                var r = b.Decompress(BucketCompressionAlgorithm.ZLib);


                byte[] chars34 = new byte[34];
                r.ReadFull(chars34);

                Assert.AreEqual("blob 26\0ABCDEFGHIJKLMNOPQRSTUVWXYZ", Encoding.ASCII.GetString(chars34));

                var v = await r.ReadAsync();

                v = await b.ReadAsync();
                Assert.IsFalse(v.IsEof);
                Assert.AreEqual(3, v.Length);
            }

            b = git_blob_ABCDEFGHIJKLMNOPQRSTUVWXYZ.AsBucket().Append(new byte[] { 255, 100, 101 }.AsBucket()).ToArray().AsBucket().PerByte();

            {
                var r = b.Decompress(BucketCompressionAlgorithm.ZLib);


                byte[] chars34 = new byte[34];
                r.ReadFull(chars34);

                Assert.AreEqual("blob 26\0ABCDEFGHIJKLMNOPQRSTUVWXYZ", Encoding.ASCII.GetString(chars34));

                var v = await r.ReadAsync();

                v = await b.ReadAsync();
                Assert.IsFalse(v.IsEof);
                Assert.AreEqual(1, v.Length);
            }
        }

        [TestMethod]
        public async Task SkipTake()
        {
            var b = Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZ").AsBucket();

            b = b.Skip(4).Take(10);

            var p = b.Peek();

            Assert.AreEqual(10, p.Length);
            Assert.AreEqual("EFGHIJKLMN", Encoding.ASCII.GetString(p.ToArray()));


            p = await b.ReadAsync(12);

            Assert.AreEqual(10, p.Length);
            Assert.AreEqual("EFGHIJKLMN", Encoding.ASCII.GetString(p.ToArray()));
        }

        [TestMethod]
        public void ReadAsStream()
        {
            var b = Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZ").AsBucket();

            using var s = b.AsStream();

            Assert.AreEqual(26, s.Length);
            Assert.AreEqual(0, s.Position);

            Assert.AreEqual((int)'A', s.ReadByte());

            Assert.AreEqual(26, s.Length);
            Assert.AreEqual(1, s.Position);
        }

        [TestMethod]
        public void ReadAsReader()
        {
            var b = Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZ").AsBucket();

            using var s = b.AsReader();

            var all = s.ReadToEnd();
            Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ", all);


            var b2 = Encoding.ASCII.GetBytes("ABCD\nEFGHI\rJKLMNOPQ\r\nRSTUV\0WXYZ").AsBucket();

            using var s2 = b2.AsReader();

            List<string> lines = new List<string>();


            while (s2.ReadLine() is string line)
            {
                lines.Add(line);
            }

            Assert.IsTrue(lines.Count >= 4);
            Assert.AreEqual("ABCD", lines[0]);
            Assert.AreEqual("EFGHI", lines[1]);
            Assert.AreEqual("JKLMNOPQ", lines[2]);
        }        

        [TestMethod]
        public async Task EnumFew()
        {
            var list = Enumerable.Range(0, 22).ToArray();

            var vq = list.AsSyncAndAsyncQueryable();

            // Legacy walk
            foreach (var v in vq)
            {

            }

            Assert.AreEqual(22, vq.Count());

            // Wrapped async walk
            await foreach (var v in vq)
            {

            }

            // Filter (compile time duplicate check)
            await foreach (var v in vq.Where(x => x > 5))
            {

            }

            // Selection (compile time duplicate check)
            await foreach (var v in vq.Select(x => x + 12))
            {

            }

            // Usable by async linq
            //await foreach (var v in vq.WhereAwait(x => new ValueTask<bool>(x == 12)))
            //{
            //
            //}

            await foreach (var v in vq.OrderByDescending(x => x))
            {

            }
        }

        [TestMethod]
        [DataRow(typeof(short))]
        [DataRow(typeof(ushort))]
        [DataRow(typeof(int))]
        [DataRow(typeof(uint))]
        [DataRow(typeof(long))]
        [DataRow(typeof(ulong))]
        public void BitConverterTests(Type tp)
        {
            long v = 1;
            bool restart = true;

            for (int i = 0; i < 100; i++)
            {
                object origValue;
                try
                {
                    v = checked(v * 3); // Triggers OverFlowException when overflowing long

                    origValue = Convert.ChangeType(v, tp);
                }
                catch (OverflowException)
                {
                    if (restart)
                    {
                        restart = false;
                        v = -1;
                        continue;
                    }

                    break;
                }

                var m = typeof(NetBitConverter).GetMethod(nameof(NetBitConverter.GetBytes), new[] { tp })!;

                var bytes = (byte[])m.Invoke(null, new[] { origValue })!;


                var m2 = typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] { tp })!;
                var bytes2 = (byte[])m2.Invoke(null, new[] { origValue })!;


                bytes2.ReverseInPlaceIfLittleEndian();

                Assert.IsTrue(bytes.SequenceEqual(bytes2), $"Reversed of for {v}");


                var rm = typeof(NetBitConverter).GetMethod("To" + tp.Name, new[] { typeof(byte[]), typeof(int) })!;
                object v1 = rm.Invoke(null, new object[] { bytes, 0 })!;


                var rm2 = typeof(BitConverter).GetMethod("To" + tp.Name, new[] { typeof(byte[]), typeof(int) })!;
                object v2 = rm2.Invoke(null, new object[] { bytes, 0 })!;

                long vv1 = Convert.ToInt64(v1);

                Assert.AreEqual(v, vv1);

                var th = typeof(NetBitConverter).GetMethod("FromNetwork", new[] { tp })!;
                Assert.AreEqual(origValue, th.Invoke(null, new[] { th.Invoke(null, new[] { origValue }) }));

                var v3 = th.Invoke(null, new[] { v2 });

                Assert.AreEqual(v1, v3);
            }

            Assert.IsFalse(restart, $"Did negative check (v={v})");
        }


        [DataRow(BucketCompressionAlgorithm.ZLib)]
        [DataRow(BucketCompressionAlgorithm.Deflate)]
        [DataRow(BucketCompressionAlgorithm.GZip)]
#if NET6_0_OR_GREATER
        [DataRow(BucketCompressionAlgorithm.Brotli)]
#endif
        [TestMethod]
        public async Task CompressionTest(BucketCompressionAlgorithm alg)
        {
            bool overshoot = false;
            var baseStream = new MemoryStream();
            for (int i = 0; i < 10; i++)
            {
                baseStream.Write(Guid.NewGuid().ToByteArray(), 0, 16);
            }

            for (int i = 0; i < 10; i++)
            {
                baseStream.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 8);
            }

            var baseData = baseStream.ToArray();

            Bucket compressed;

            switch (alg)
            {
                case BucketCompressionAlgorithm.ZLib:
#if !NET6_0_OR_GREATER
                    compressed = baseData.AsBucket().Compress(alg);
#else
                    {
                        var ms = new MemoryStream();
                        var zs = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionLevel.Optimal);

                        zs.Write(baseData, 0, baseData.Length);
                        zs.Close();
                        compressed = ms.ToArray().AsBucket();
                    }
#endif
                    break;

                case BucketCompressionAlgorithm.GZip:
                    {
                        var ms = new MemoryStream();
                        var zs = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal);

                        zs.Write(baseData, 0, baseData.Length);
                        zs.Close();
                        compressed = ms.ToArray().AsBucket();
                        break;
                    }
                case BucketCompressionAlgorithm.Deflate:
                    {
                        var ms = new MemoryStream();
                        var zs = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal);

                        zs.Write(baseData, 0, baseData.Length);
                        zs.Close();
                        compressed = ms.ToArray().AsBucket();
                        break;
                    }
#if NET6_0_OR_GREATER
                case BucketCompressionAlgorithm.Brotli:
                    {
                        var ms = new MemoryStream();
                        var zs = new System.IO.Compression.BrotliStream(ms, System.IO.Compression.CompressionLevel.Optimal);

                        zs.Write(baseData, 0, baseData.Length);
                        zs.Close();
                        compressed = ms.ToArray().AsBucket();

                        overshoot = true;
                        break;
                    }
#endif
                default:
                    throw new InvalidOperationException();
            }

            var finishData = overshoot ? Array.Empty<byte>() : Guid.NewGuid().ToByteArray();
            var compressedData = await compressed.Append(finishData.AsBucket()).ToArrayAsync();

            ushort firstTwo = NetBitConverter.ToUInt16(compressedData, 0);

            switch (alg)
            {
                case BucketCompressionAlgorithm.GZip:
                    Assert.AreEqual(0x1F8B, firstTwo, $"Got 0x{firstTwo:x4}");
                    break;
                case BucketCompressionAlgorithm.ZLib:
                    bool isZlib = new ushort[] { 0x7801, 0x789C, 0x78da }.Contains(firstTwo);
                    Assert.IsTrue(isZlib, $"Got 0x{firstTwo:x4}");
                    break;
                case BucketCompressionAlgorithm.Deflate:
                    // FirstTwo can be anything
                    break;
            }


            var inner = compressedData.AsBucket();
            var bb = await inner.Decompress(alg).ReadFullAsync(4096);

            Assert.AreEqual(baseData.Length, bb.Length);

            var decompressed = bb.ToArray();

            Assert.IsTrue(decompressed.SequenceEqual(baseData), "Same data after decompression");

            bb = await inner.ReadFullAsync(4096);
            Assert.AreEqual(finishData.Length, bb.Length);
            Assert.IsTrue(bb.ToArray().SequenceEqual(finishData));

            var r = await baseData.AsBucket().Compress(alg).ToArrayAsync();
            Stream rs;
            switch (alg)
            {
                case BucketCompressionAlgorithm.ZLib:
                    rs = r.AsBucket().Decompress(BucketCompressionAlgorithm.ZLib).AsStream();
                    break;
                case BucketCompressionAlgorithm.GZip:
                    rs = new System.IO.Compression.GZipStream(new MemoryStream(r), System.IO.Compression.CompressionMode.Decompress);
                    break;
                case BucketCompressionAlgorithm.Deflate:
                    rs = new System.IO.Compression.DeflateStream(new MemoryStream(r), System.IO.Compression.CompressionMode.Decompress);
                    break;
#if NET6_0_OR_GREATER
                case BucketCompressionAlgorithm.Brotli:
                    rs = new System.IO.Compression.BrotliStream(new MemoryStream(r), System.IO.Compression.CompressionMode.Decompress);
                    break;
#endif
                default:
                    throw new InvalidOperationException();
            }

            byte[] resultBytes = new byte[4096];
            Assert.AreEqual(baseData.Length, rs.Read(resultBytes, 0, resultBytes.Length));
        }

        [TestMethod]
        public void VerifyDebuggerDisplayAttribute()
        {
            foreach(var type in typeof(Bucket).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(DebuggerDisplayAttribute), false)?.FirstOrDefault() is DebuggerDisplayAttribute dd)
                {
                    foreach(var q in Regex.Matches(dd.Value, "(?<={)[^}]+(?=})").Cast<Match>().Select(x=>x.Value))
                    {
                        int n = q.IndexOfAny(new[] { ',', ':', '(' });

                        string name = (n >= 0) ? q.Substring(0, n) : q;

                        var f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

                        if (type.GetField(name, f) is null && type.GetProperty(name, f) is null)
                            Assert.Fail($"Member {name} referenced in {nameof(DebuggerDisplayAttribute)} on {type.FullName} not found {dd.Value}");
                    }
                }
            }
        }
    }
}
