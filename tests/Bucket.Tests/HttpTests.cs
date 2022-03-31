﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AmpScm.Buckets.Client.Http;
using BucketTests;

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
        public async Task GetCloudFlareHome()
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
                TestContext.WriteLine($"HTTP/1.1 {hrb.HttpStatus} {hrb.HttpMessage}");
                TestContext.WriteLine(result.Headers.ToString());
            }

            while (!(bb = await result.ReadAsync()).IsEof)
            {
                var t = bb.ToUTF8String();
                len += bb.Length;
                //TestContext.WriteLine(t);
                total += t;
            }
        }

#if !DEBUG
        [Timeout(20000)]
#endif
        [TestMethod]
        public async Task GetGitHubHomeInsecure()
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
                TestContext.Write(t);
                total += t;
            }
        }
    }
}