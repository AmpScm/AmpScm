﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    public static partial class GitIndexer
    {
        public static async ValueTask<GitId> IndexPack(string packFile, bool writeReverseIndex = false, bool writeBitmap = false, GitIdType idType = GitIdType.Sha1, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(packFile))
                throw new ArgumentNullException(nameof(packFile));

            DateTime start = DateTime.Now;

            if (writeBitmap)
                throw new NotImplementedException();

            using var srcFile = FileBucket.OpenRead(packFile, false);
            var hashes = new SortedList<GitId, (long Offset, int CRC)>();

            async ValueTask<GitObjectBucket?> GetDeltaSource(GitId id)
            {
                if (hashes.TryGetValue(id, out var v))
                {
                    var d = srcFile.Duplicate();
                    await d.SeekAsync(v.Offset).ConfigureAwait(false);

                    return new GitPackObjectBucket(d, idType, GetDeltaSource);
                }
                return null;
            }

            long objectCount;
            using (var gh = new GitPackHeaderBucket(srcFile.NoClose()))
            {
                var r = await gh.ReadAsync().ConfigureAwait(false);
                objectCount = (int)gh.ObjectCount!.Value;
            }

            List<long> fixUpLater = new List<long>();

            for (int i = 0; i < objectCount; i++)
            {
                GitId? oid = null;
                int crc = 0;
                long offset = srcFile.Position!.Value;

                using (var pf = new GitPackObjectBucket(srcFile.NoClose().Crc32(c => crc = c), idType, id => new(DummyObjectBucket.Instance)))
                {

                    if (await pf.ReadNeedsBaseAsync().ConfigureAwait(false))
                        await pf.ReadSkipAsync(long.MaxValue).ConfigureAwait(false);
                    else
                    {
                        var type = await pf.ReadTypeAsync().ConfigureAwait(false);
                        var len = await pf.ReadRemainingBytesAsync().ConfigureAwait(false);

                        using var csum = (type.CreateHeader(len.Value) + pf.NoClose()).GitHash(idType, s => oid = s);

                        await csum.ReadSkipAsync(long.MaxValue).ConfigureAwait(false);
                    }
                }

                if (oid != null)
                {
                    hashes[oid] = (offset, crc);
                }
                else
                    fixUpLater.Add(offset);
            }

            Console.WriteLine(DateTime.Now - start);

            var packChecksum = await srcFile.ReadGitIdAsync(idType).ConfigureAwait(false);

            while (fixUpLater.Count > 0)
            {
                int n = fixUpLater.Count;
                Console.WriteLine($"{fixUpLater.Count} / {hashes.Count}");

                List<long> load = new(fixUpLater);
                fixUpLater.Clear();

                Func<long, CancellationToken, ValueTask> cb = async (offset, c) =>
                {
                    using (var b = srcFile.Duplicate(true))
                    {
                        await b.SeekAsync(offset).ConfigureAwait(false);

                        await IndexOne(offset, b).ConfigureAwait(false);
                    }
                };

#if NET6_0_OR_GREATER
                await Parallel.ForEachAsync(load, cb).ConfigureAwait(false);
#else
                foreach (var v in load)
                    await cb(v, cancellationToken).ConfigureAwait(false);
#endif
            }
            Console.WriteLine(DateTime.Now - start);

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
            index += fanOut.Select(x => NetBitConverter.GetBytes(x)).AsBucket();
            // Hashes
            index += hashes.Keys.Select(x => x.Hash).AsBucket();

            //TestContext.WriteLine($"CRCs start at {await index.ReadRemainingBytesAsync()}");
            // CRC32 values of packed data
            index += hashes.Values.Select(x => NetBitConverter.GetBytes(x.Item2)).AsBucket();
            // File offsets
            index += hashes.Values.Select(x => NetBitConverter.GetBytes((uint)x.Item1)).AsBucket();

            index += packChecksum.Hash!.AsBucket();

            {
                using var idxFile = File.Create(Path.ChangeExtension(packFile, ".idx"));

                GitId? idxChecksum = null;
                await index.GitHash(idType, x => idxChecksum = x).WriteToAsync(idxFile).ConfigureAwait(false);

                await idxFile.WriteAsync(idxChecksum!.Hash).ConfigureAwait(false);
            }

            if (writeReverseIndex)
            {
                byte[] mapBytes;

                mapBytes = new byte[sizeof(uint) * hashes.Count];
                int n = 0;
                foreach (var v in hashes.Select((kv, n) => new { kv.Value.Offset, Index = n }).OrderBy(x => x.Offset))
                {
                    Array.Copy(NetBitConverter.GetBytes(v.Index), 0, mapBytes, n, sizeof(uint));

                    n += sizeof(uint);
                }

                GitId? sha = null;
                using (var fs = File.Create(Path.ChangeExtension(packFile, ".rev")))
                {
                    await (Encoding.ASCII.GetBytes("RIDX").AsBucket()
                                + NetBitConverter.GetBytes((int)1 /* Version 1 */).AsBucket()
                                + NetBitConverter.GetBytes((int)idType).AsBucket()
                                + mapBytes.AsBucket()
                                + packChecksum.Hash.AsBucket())
                            .GitHash(idType, r => sha = r)
                        .WriteToAsync(fs).ConfigureAwait(false);

                    await fs.WriteAsync(sha!.Hash).ConfigureAwait(false);
                }
            }

            return packChecksum;

            async Task<bool> IndexOne(long offset, Bucket src)
            {
                int crc = 0;
                GitId? checksum = null;
                bool skipNoHash = false;

                GitObjectBucket FixUp(GitId id)
                {
                    skipNoHash = true;
                    return DummyObjectBucket.Instance;
                }

                var pf = new GitPackObjectBucket(src.NoClose().Crc32(c => crc = c), idType, async id => await GetDeltaSource(id).ConfigureAwait(false) ?? FixUp(id));

                var type = await pf.ReadTypeAsync().ConfigureAwait(false);

                if (skipNoHash)
                {
                    pf.Dispose();
                    lock (fixUpLater)
                    {
                        fixUpLater.Add(offset);
                    }

                    return false;
                }
                else
                {
                    var len = await pf.ReadRemainingBytesAsync().ConfigureAwait(false);

                    using var csum = (type.CreateHeader(len.Value) + pf).GitHash(idType, s => checksum = s);

                    await csum.ReadSkipAsync(long.MaxValue).ConfigureAwait(false);

                    lock (fixUpLater)
                    {
                        hashes.Add(checksum!, (offset, crc));
                    }
                    return true;
                }
            }
        }

        sealed class DummyObjectBucket : GitObjectBucket
        {
            private DummyObjectBucket(Bucket inner) : base(inner)
            {
            }

            public static DummyObjectBucket Instance { get; } = new(Bucket.Empty);

            public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
            {
                return Inner.ReadAsync(requested);
            }

            public override ValueTask<GitObjectType> ReadTypeAsync()
            {
                return new(GitObjectType.None);
            }
        }
    }
}
