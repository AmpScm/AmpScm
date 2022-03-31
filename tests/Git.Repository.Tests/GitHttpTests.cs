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
using AmpScm.Git;
using AmpScm.Buckets.Git;
using BucketTests;

namespace GitRepositoryTests
{
    [TestClass]
    public class GitHttpTests
    {
        public TestContext TestContext { get; set; } = null!;

        public BucketWebClient Client { get; } = new();


#if !DEBUG
        [Timeout(20000)]
#endif
        [TestMethod]
        public async Task GetGitInfoV1()
        {
            var br = Client.CreateRequest($"https://github.com/rhuijben/putty.git/info/refs?service=git-upload-pack");

            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            //br.Headers["Git-Protocol"] = "version=2";
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

            var pkt = new GitPacketBucket(result);

            while (!(bb = await pkt.ReadFullPacket()).IsEof)
            {
                TestContext.WriteLine($"-- {pkt.CurrentPacketLength} --");

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
        public async Task GetGitInfoV2()
        {
            var br = Client.CreateRequest($"https://github.com/rhuijben/putty.git/info/refs?service=git-upload-pack");

            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            br.Headers["Git-Protocol"] = "version=2";
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

            var pkt = new GitPacketBucket(result);

            while (!(bb = await pkt.ReadFullPacket()).IsEof)
            {
                TestContext.WriteLine($"-- {pkt.CurrentPacketLength} --");

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
        public async Task GetGitInfoV2Auth()
        {
            using var rp = GitRepository.Open(Environment.CurrentDirectory);

            var br = Client.CreateRequest($"https://github.com/rhuijben/asd-admin-css.git/info/refs?service=git-upload-pack");
            //br.PreAuthenticate = true;

            br.BasicAuthentication += (sender, e) => { e.Username = $"q-{Guid.NewGuid()}"; e.Password = "123"; e.Handled = true; };
            //br.BasicAuthentication += rp.Configuration.BasicAuthenticationHandler;


            br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
            br.Headers["Git-Protocol"] = "version=2";
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

            var pkt = new GitPacketBucket(result);

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