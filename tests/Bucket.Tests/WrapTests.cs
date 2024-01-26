using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Client;
using AmpScm.Buckets.Specialized;
using AmpScm.BucketTests.Buckets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BucketTests;

[TestClass]
public class WrapTests
{
    public TestContext TestContext { get; set; } = default!;

    public static IEnumerable<object[]> ConvertBuckets
        =>
        new ValueTuple<string, Func<Bucket, Bucket>, Func<Bucket, Bucket>>[]
        {
            ("Base64", b => b.Base64Encode(), b => b.Base64Decode()),
            ("ZLib", b => b.Compress(BucketCompressionAlgorithm.ZLib), b => b.Decompress(BucketCompressionAlgorithm.ZLib)),
            ("deflate", b => b.Compress(BucketCompressionAlgorithm.Deflate), b => b.Decompress(BucketCompressionAlgorithm.Deflate)),
            ("GZip", b => b.Compress(BucketCompressionAlgorithm.GZip), b => b.Decompress(BucketCompressionAlgorithm.GZip)),
#if !NETFRAMEWORK
            ("Brotli", b => b.Compress(BucketCompressionAlgorithm.Brotli), b => b.Decompress(BucketCompressionAlgorithm.Brotli)),
#endif
            ("Chunk", b => b.HttpChunk(), b => b.HttpDechunk()),
            ("AsStream", b => b.AsStream().AsBucket(), b => b.AsStream().AsBucket()),
            ("Utf16-Utf8", b => b.TextRecode(Encoding.UTF8, Encoding.UTF32),  b => b.TextRecode(Encoding.UTF32, Encoding.UTF8)),
        }.Select(x => new object[] { x.Item1, x.Item2, x.Item3 });

    public static string ConvertDisplayName(MethodInfo method, object[] args)
    {
        return $"{method.Name}({args[0]})";
    }

    private static Bucket MakeBucket(string name, Bucket over)
    {
        Type tp = typeof(Bucket).Assembly.GetType(name)!;

        return (Bucket)Activator.CreateInstance(tp, over)!;
    }

    [TestMethod]
    [DynamicData(nameof(ConvertBuckets), DynamicDataDisplayName = nameof(ConvertDisplayName))]
    public async Task TestConvert(string name, Func<Bucket, Bucket> encode, Func<Bucket, Bucket> decode)
    {
        {
            byte[] sourceData = Encoding.UTF8.GetBytes($"This is some test data for the {nameof(TestConvert)} function");

            Bucket src = encode(sourceData.AsBucket());

            var dest = decode(src);

            byte[] resultData = await dest.ToArrayAsync();

            Assert.IsTrue(resultData.SequenceEqual(sourceData), "Data was properly converted back");
        }

        for (int i = 10; i >= 0; i--)
        {
            byte[] sourceData = Encoding.UTF8.GetBytes("".PadLeft(i, 'Q'));

            Bucket src = encode(sourceData.AsBucket());

            var dest = decode(src);

            byte[] resultData = await dest.ToArrayAsync();

            Assert.IsTrue(resultData.SequenceEqual(sourceData), "Data was properly converted back");
        }

        {
            var sd = Enumerable.Range(0, 30).Select(x => Enumerable.Range(0, 40).Select(y => (byte)x).ToArray().AsBucket()).AsBucket();
            var alt = Enumerable.Range(0, 30).SelectMany(x => Enumerable.Range(0, 40).Select(y => (byte)x)).ToArray();

            Bucket src = encode(sd);

            var dest = decode(src);

            byte[] resultData = await dest.ToArrayAsync();

            Assert.IsTrue(resultData.SequenceEqual(alt), "Data was properly converted back");

            var bb = await src.ReadAsync();
            Assert.IsTrue(bb.IsEof);
        }
    }

    [TestMethod]
    [DynamicData(nameof(ConvertBuckets), DynamicDataDisplayName = nameof(ConvertDisplayName))]
    public async Task TestConvertFail(string name, Func<Bucket, Bucket> encode, Func<Bucket, Bucket> decode)
    {
        var sd = Enumerable.Range(0, 30).Select(x => Enumerable.Range(0, 40).Select(y => (byte)y).ToArray().AsBucket()).AsBucket();
        var alt = Enumerable.Range(0, 30).SelectMany(x => Enumerable.Range(0, 40).Select(y => (byte)y)).ToArray();

        List<byte> save = new List<byte>();
        {
            Bucket src = encode(sd);
            while ((await src.ReadByteAsync()) is byte b)
            {
                save.Add(b);
            }


            var bb = await src.ReadAsync();
            Assert.IsTrue(bb.IsEof);
        }

        {
            var dest = decode(save.ToArray().AsBucket());

            byte[] resultData = await dest.ToArrayAsync();
            Assert.AreEqual(alt.Length, resultData.Length);

            Assert.IsTrue(resultData.SequenceEqual(alt), "Data was properly converted back");

        }
        {
            var dest = decode(save.AsBucket()).PerByte();

            byte[] resultData = await dest.ToArrayAsync();
            Assert.AreEqual(alt.Length, resultData.Length);

            Assert.IsTrue(resultData.SequenceEqual(alt), "Data was properly converted back per byte");
        }
    }

    [TestMethod]
    public async Task TestConvertFail2()
    {
        var sd = new byte[] { 1, 2, 3 }.AsBucket() + new byte[] { 11, 12, 13 }.AsBucket();
        var alt = new byte[] { 1, 2, 3, 11, 12, 13 };

        Bucket src = sd.Compress(BucketCompressionAlgorithm.ZLib);

        List<byte> save = new List<byte>();

        while ((await src.ReadByteAsync()) is byte b)
        {
            save.Add(b);
        }

        var dest = save.AsBucket().Decompress(BucketCompressionAlgorithm.ZLib);

        byte[] resultData = await dest.ToArrayAsync();
        Assert.AreEqual(alt.Length, resultData.Length);

        Assert.IsTrue(resultData.SequenceEqual(alt), "Data was properly converted back");

        var bb = await src.ReadAsync();
        Assert.IsTrue(bb.IsEof);
    }

    [TestMethod]
    public async Task TestBase64Encoding()
    {
        foreach (string src in new[] {
            $"Some Other Data for the {nameof(TestBase64Encoding)} function",
            "Aap aap aap",
            "1234",
            "123",
            "12",
            "1",
            "".PadRight(1024, 'A')})
        {
            byte[] sourceData = Encoding.UTF8.GetBytes(src);

            byte[] encoded = await sourceData.AsBucket().Base64Encode().ToArrayAsync();
            var expectedData = Convert.ToBase64String(sourceData);

            BucketBytes bb = encoded;
            Assert.AreEqual(expectedData, bb.ToUTF8String(), $"Encode Equal for {src}");

            byte[] decoded = await encoded.AsBucket().Base64Decode().ToArrayAsync();
            bb = decoded;
            Assert.AreEqual(src, bb.ToUTF8String(), $"Decode Equal for {src}");


            encoded = await sourceData.AsBucket().Base64Encode(true).ToArrayAsync();
            expectedData = Convert.ToBase64String(sourceData, Base64FormattingOptions.InsertLineBreaks);

            bb = encoded;
            Assert.AreEqual(expectedData, bb.ToUTF8String(), $"Encode Equal for {src}");

            decoded = await encoded.AsBucket().Base64Decode().ToArrayAsync();
            bb = decoded;
            Assert.AreEqual(src, bb.ToUTF8String(), $"Decode Equal for {src}");
        }

    }

    //[TestMethod]
    //public void WriteMap()
    //{
    //    var b = Base64DecodeBucket.CalculateReverseBase64Map();
    //    for (int i = 0; i < b.Length; i++)
    //    {
    //        for(int j = 0; j < 4 && i < b.Length; j++, i++)
    //        {
    //            TestContext.Write($" /* '{(char.IsControl((char)i) ? "\\x" + i.ToString("x") : (char)(i))}': */".PadRight(15, ' '));
    //            TestContext.Write($" {b[i]}".PadLeft(3));
    //            if (i < b.Length - 1)
    //                TestContext.Write(",");
    //        }
    //        TestContext.WriteLine("");
    //        i--;
    //    }
    //
    //}

    [TestMethod]
    public async Task CRC32Tests()
    {
        string srcText = "Deze banaan wil graag een CRCtje!";

        int hash = 0;

        await Bucket.Create.FromUTF8(srcText).Crc32(x => hash = x).ReadUntilEofAsync();

        Crc32 crc32 = new Crc32();

        crc32.Append(Encoding.UTF8.GetBytes(srcText));

        var d2 = crc32.GetHashAndReset();

        var hash2 = BitConverter.ToInt32(d2, 0);

        Assert.AreEqual(hash, hash2);
    }

}
