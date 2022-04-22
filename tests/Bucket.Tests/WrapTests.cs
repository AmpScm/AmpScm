using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BucketTests
{
    [TestClass]
    public class WrapTests
    {
        public TestContext TestContext { get; set; } = default!;

        public static IEnumerable<object[]> ConvertBuckets
            =>
            new ValueTuple<string, Func<Bucket, Bucket>, Func<Bucket, Bucket>>[]
            {
                ("Base64", b => b.Base64Encode(), b => b.Base64Decode()),
                ("ZLib", b => b.Compress(BucketCompressionAlgorithm.ZLib), b=> b.Decompress(BucketCompressionAlgorithm.ZLib)),
                //("deflate", b => b.Compress(BucketCompressionAlgorithm.Deflate), b=> b.Decompress(BucketCompressionAlgorithm.Deflate)),
                ("GZip", b => b.Compress(BucketCompressionAlgorithm.GZip), b=> b.Decompress(BucketCompressionAlgorithm.GZip)),
#if !NETFRAMEWORK
                ("Brotli", b => b.Compress(BucketCompressionAlgorithm.Brotli), b=> b.Decompress(BucketCompressionAlgorithm.Brotli)),
#endif
                ("Chunk", b => MakeBucket("AmpScm.Buckets.Client.Http.HttpChunkBucket", b), b => MakeBucket("AmpScm.Buckets.Client.Http.HttpDechunkBucket", b))
            }.Select(x => new object[] { x.Item1, x.Item2, x.Item3 });

        public static string ConvertDisplayName(MethodInfo method, object[] args)
        {
            return $"{method.Name}({args[0]})";
        }

        static Bucket MakeBucket(string name, Bucket over)
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

                using var dest = decode(src);

                byte[] resultData = await dest.ToArrayAsync();

                Assert.IsTrue(resultData.SequenceEqual(sourceData), "Data was properly converted back");
            }

            for(int i = 10; i >= 0; i--)
            {
                byte[] sourceData = Encoding.UTF8.GetBytes("".PadLeft(i, 'Q'));

                Bucket src = encode(sourceData.AsBucket());

                using var dest = decode(src);

                byte[] resultData = await dest.ToArrayAsync();

                Assert.IsTrue(resultData.SequenceEqual(sourceData), "Data was properly converted back");
            }
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
                "1"})
            {
                byte[] sourceData = Encoding.UTF8.GetBytes(src);

                byte[] encoded = await sourceData.AsBucket().Base64Encode().ToArrayAsync();
                var expectedData = Convert.ToBase64String(sourceData);

                BucketBytes bb = encoded;
                Assert.AreEqual(expectedData, bb.ToUTF8String(), $"Encode Equal for {src}");

                byte[] decoded = await encoded.AsBucket().Base64Decode().ToArrayAsync();
                bb = decoded;
                Assert.AreEqual(src, bb.ToUTF8String(), $"Decode Equal for {src}");
            }

        }

    }
}
