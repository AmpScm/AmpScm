using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AmpScm;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests
{
    [TestClass]
    public class GitTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public async Task WalkSvnPack()
        {
            string packFile = Directory.GetFiles(Path.Combine(GitTestEnvironment.GetRepository(GitTestDir.Packed), ".git/objects/pack"), "*.pack").First();

            byte[]? fileChecksum = null;
            var srcFile = FileBucket.OpenRead(packFile, false);

            long l = (await srcFile.ReadRemainingBytesAsync()).Value;

            using var b = srcFile.TakeExactly(l - 20).SHA1(x => fileChecksum = x);

            var gh = new GitPackHeaderBucket(b.NoDispose());

            var r = await gh.ReadAsync();
            Assert.IsTrue(r.IsEof);

            Assert.AreEqual("PACK", gh.GitType);
            Assert.AreEqual(2, gh.Version);
            //Assert.AreEqual(70, gh.ObjectCount);
            TestContext.WriteLine("sha1 type body-length entry-length offset [delta-count]");

            var hashes = new SortedList<GitId, (long, int)>();
            for (int i = 0; i < gh.ObjectCount; i++)
            {
                long? offset = b.Position;
                GitId? checksum = null;
                int crc = 0;
                var crcr = b.NoDispose().Crc32(c => crc = c);
                using (var pf = new GitPackObjectBucket(crcr, GitIdType.Sha1, id => GetDeltaSource(packFile, id)))
                {

                    var type = await pf.ReadTypeAsync();

                    var len = await pf.ReadRemainingBytesAsync();

                    Assert.AreEqual(0L, pf.Position);



                    var hdr = type.CreateHeader(len.Value);
                    var hdrLen = await hdr.ReadRemainingBytesAsync();

                    var csum = hdr.Append(pf.NoDispose()).GitHash(GitIdType.Sha1, s => checksum = s);

                    var data = await csum.ReadToEnd();


                    TestContext.Write(checksum?.ToString());

                    TestContext.Write($" {type.ToString().ToLowerInvariant(),-6} {pf.BodySize} {b.Position - offset} {offset}");
                    int deltaCount = await pf.ReadDeltaCountAsync();
                    if (deltaCount > 0)
                        TestContext.Write($" {deltaCount} delta (body={len})");
                    else
                        Assert.AreEqual(pf.BodySize, len);

                    Assert.AreEqual(len.Value + hdrLen, data.Length, "Can read provided length bytes");
                    Assert.AreEqual(len.Value, pf.Position, "Expected end position");

                    TestContext.WriteLine();
                    Assert.AreEqual(0, crc); // Not calculated yet. We didn't get EOF yet

                    await pf.ReadUntilEofAsync();
                }

                hashes.Add(checksum!, (offset!.Value, crc));
            }

            var eofCheck = await b.ReadAsync();

            Assert.IsTrue(eofCheck.IsEof);
            Assert.IsNotNull(fileChecksum);

            int[] fanOut = new int[256];

            foreach (var v in hashes)
            {
                fanOut[v.Key[0]]++;
            }

            for (int i = 0, last = 0; i < fanOut.Length; i++)
            {
                last = (fanOut[i] += last);
            }

            // Let's assume v2 pack index files
            Bucket index = new byte[] { 255, (byte)'t', (byte)'O', (byte)'c' }.AsBucket();
            index += NetBitConverter.GetBytes((int)2).AsBucket();

            // Fanout table
            index += fanOut.SelectMany(x => NetBitConverter.GetBytes(x)).ToArray().AsBucket();
            // Hashes
            index += hashes.Keys.SelectMany(x => x.Hash.ToArray()).ToArray().AsBucket();

            TestContext.WriteLine($"CRCs start at {await index.ReadRemainingBytesAsync()}");
            // CRC32 values of packed data
            index += hashes.Values.Select(x => NetBitConverter.GetBytes(x.Item2)).ToArray().AsBucket();
            // File offsets
            index += hashes.Values.Select(x => NetBitConverter.GetBytes((uint)x.Item1).AsBucket()).ToArray();

            index += fileChecksum.AsBucket();

            var indexFile = FileBucket.OpenRead(Path.ChangeExtension(packFile, ".idx"), false);
            long lIdx = (await indexFile.ReadRemainingBytesAsync()).Value;

            byte[]? idxChecksum = null;
            using var idxData = indexFile.TakeExactly(lIdx - 20).SHA1(x => idxChecksum = x);

            Trace.WriteLine(packFile);
            await Assert.That.BucketsEqual(idxData, index);
        }

        private async ValueTask<GitObjectBucket?> GetDeltaSource(string packFile, GitId id)
        {
            GitRepository repo = await GitRepository.OpenAsync(Path.GetDirectoryName(packFile)!);

            return (await repo.ObjectRepository.FetchGitIdBucketAsync(id)) ?? throw new InvalidOperationException($"Can't obtain object {id} from {packFile}");
        }
    }
}
