using System;
using System.Net;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AmpScm;
using AmpScm.Buckets.Client.Buckets;

namespace BucketTests
{
    [TestClass]
    public class HttpTests
    {
        public TestContext TestContext { get; set; } = null!;

        public BucketWebClient Client { get; } = new();


#if !DEBUG
        [Timeout(20000)]
#endif
        [TestMethod]
        public async Task GetCloudFlare404()
        {
            var br = Client.CreateRequest($"https://cloudflare.com/get-404-{Guid.NewGuid()}");

            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            using var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;

            await result.ReadHeaders();

            if (result is HttpResponseBucket hrb)
            {
                TestContext.WriteLine($"HTTP/{hrb.HttpVersion} {hrb.HttpStatus} {hrb.HttpMessage}");
                TestContext.WriteLine(result.Headers.ToString());
            }

            while (!(bb = await result.ReadAsync()).IsEof)
            {
                var t = bb.ToUTF8String();
                len += bb.Length;
                TestContext.Write(t);
                total += t;
            }
        }

#if !DEBUG
        [Timeout(20000)]
#endif
        [TestMethod]
        public async Task GetGitHub404()
        {
            var br = Client.CreateRequest($"http://github.com/get-404-{Guid.NewGuid()}");

            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            using var result = await br.GetResponseAsync();

            BucketBytes bb;
            string total = "";
            int len = 0;

            await result.ReadHeaders();
            if (result is HttpResponseBucket hrb)
            {
                TestContext.WriteLine($"HTTP/1.1 {hrb.HttpStatus} {hrb.HttpMessage}");

                TestContext.WriteLine(result.Headers.ToString());
                TestContext.WriteLine();
            }

            while (!(bb = await result.ReadAsync()).IsEof)
            {
                var t = bb.ToUTF8String();
                len += bb.Length;
                //TestContext.Write(t);
                total += t;
            }
        }

        [TestMethod]
        public void TestConvertWhitespace()
        {
            try
            {
                Assert.AreEqual(10, Convert.ToInt32("10\r", 10)); // With 'fromBase', no whitespace allowed
                Assert.Fail();
            }
            catch (FormatException)
            {

            }

            Assert.AreEqual(13, Convert.ToInt32("13\r")); // Without, no problem
        }
    }
}
