using System.Net;
using AmpScm;
using AmpScm.Buckets;
using AmpScm.Buckets.Client;
using AmpScm.Buckets.Client.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Git;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests;

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
#if NETFRAMEWORK
#if NET48_OR_GREATER
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
#endif
            return; // Results not stable on MONO, nor .Net 4.7
#endif

        var br = Client.CreateRequest($"https://github.com/rhuijben/putty.git/info/refs?service=git-upload-pack");

        br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
        //br.Headers["Git-Protocol"] = "version=2";
        await using var result = await br.GetResponseAsync();

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
#if NETFRAMEWORK
#if NET48_OR_GREATER
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
#endif
            return; // Results not stable on MONO, nor .Net 4.7
#endif

        var br = Client.CreateRequest($"https://github.com/rhuijben/putty.git/info/refs?service=git-upload-pack");

        br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
        br.Headers["Git-Protocol"] = "version=2";
        await using var result = await br.GetResponseAsync();

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
#if NETFRAMEWORK
#if NET48_OR_GREATER
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
#endif
            return; // Results not stable on MONO, nor .Net 4.7
#endif

        using var rp = GitRepository.Open(Environment.CurrentDirectory);

        var br = Client.CreateRequest($"https://github.com/rhuijben/asd-admin-css.git/info/refs?service=git-upload-pack");
        //br.PreAuthenticate = true;

        br.BasicAuthentication += (sender, e) => { e.Username = $"q-{Guid.NewGuid()}"; e.Password = "123"; e.Handled = true; };
        //br.BasicAuthentication += rp.Configuration.BasicAuthenticationHandler;


        br.Headers[HttpRequestHeader.UserAgent] = "BucketTest/0 " + TestContext.TestName;
        br.Headers["Git-Protocol"] = "version=2";
        await using var result = await br.GetResponseAsync();

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
