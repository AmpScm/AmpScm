﻿using System.Diagnostics;
using System.Text;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git;

public static partial class GitIndexer
{
    public static async ValueTask<GitId> IndexPack(string packFile, bool writeReverseIndex = false, bool writeBitmap = false, GitIdType idType = GitIdType.Sha1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(packFile))
            throw new ArgumentNullException(nameof(packFile));

        DateTime start = DateTime.Now;

        if (writeBitmap)
            throw new NotSupportedException();

        await using var srcFile = FileBucket.OpenRead(packFile, forAsync: false);
        var hashes = new SortedList<GitId, long>();
        var crcs = new SortedList<long, (int CRC, List<long> Deps)>();

        async ValueTask<GitObjectBucket?> GetDeltaSource(GitId id)
        {
            long offset;
            lock (hashes)
            {
                if (!hashes.TryGetValue(id, out offset))
                    return null;
            }

            var d = await SpecializedBucketExtensions.DuplicateSeekedAsync(srcFile, offset).ConfigureAwait(false);

            return new GitPackObjectBucket(d, idType, GetDeltaSource);
        }

        long objectCount;
        await using (var gh = new GitPackHeaderBucket(srcFile.NoDispose()))
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

            cancellationToken.ThrowIfCancellationRequested();

            await using (var pf = new GitPackObjectBucket(srcFile.NoDispose().Crc32(c => crc = c), idType,
                id => new(DummyObjectBucket.Instance),
                ofs => { crcs[ofs].Deps.Add(offset); return new(DummyObjectBucket.Instance); }))
            {

                if (await pf.ReadNeedsBaseAsync().ConfigureAwait(false))
                    await pf.ReadSkipAsync(long.MaxValue).ConfigureAwait(false);
                else
                {
                    var type = await pf.ReadTypeAsync().ConfigureAwait(false);
                    var len = await pf.ReadRemainingBytesAsync().ConfigureAwait(false);

                    await using var csum = (type.CreateHeader(len.Value) + pf.NoDispose()).GitHash(idType, s => oid = s);

                    await csum.ReadSkipAsync(long.MaxValue).ConfigureAwait(false);
                }
            }

            crcs[offset] = (crc, new());
#pragma warning disable CA1508 // Avoid dead conditional code
            if (oid != null)
#pragma warning restore CA1508 // Avoid dead conditional code
            {
                hashes[oid] = offset;
            }
            else
                fixUpLater.Add(offset);
        }

        var packChecksum = await srcFile.ReadGitIdAsync(idType).ConfigureAwait(false);

        while (fixUpLater.Count > 0)
        {
            int n = fixUpLater.Count;

            List<long> load = new(fixUpLater);
            fixUpLater.Clear();

            cancellationToken.ThrowIfCancellationRequested();

            async ValueTask callback(long offset, CancellationToken c)
            {
                var b = await SpecializedBucketExtensions.DuplicateSeekedAsync(srcFile, offset).ConfigureAwait(false);

                await IndexOne(offset, b).ConfigureAwait(false);
            }

#if !NETFRAMEWORK
            await Parallel.ForEachAsync(load, callback).ConfigureAwait(false);
#else
            foreach (var v in load)
                await callback(v, default).ConfigureAwait(false);
#endif
        }

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
        index += NetBitConverter.GetBytes(2).AsBucket();

        // Fanout table
        index += fanOut.Select(x => NetBitConverter.GetBytes(x)).AsBucket();
        // Hashes
        index += hashes.Keys.Select(x => x.Hash).AsBucket();

        // CRC32 values of packed data
        index += hashes.Values.Select(x => NetBitConverter.GetBytes(crcs[x].CRC)).AsBucket();
        // File offsets
        index += hashes.Values.Select(x => NetBitConverter.GetBytes((uint)x)).AsBucket();

        index += packChecksum.Hash!.AsBucket();

        {
            using var idxFile = File.Create(Path.ChangeExtension(packFile, ".idx"));

            GitId? idxChecksum = null;
            await index.GitHash(idType, x => idxChecksum = x).WriteToAsync(idxFile, cancellationToken).ConfigureAwait(false);

            await idxFile.WriteAsync(idxChecksum!.Hash, cancellationToken).ConfigureAwait(false);
        }

        if (writeReverseIndex)
        {
            byte[] mapBytes;

            mapBytes = new byte[sizeof(uint) * hashes.Count];
            int n = 0;
            foreach (var v in hashes.Select((kv, n) => new { Offset = kv.Value, Index = n }).OrderBy(x => x.Offset))
            {
                Array.Copy(NetBitConverter.GetBytes(v.Index), 0, mapBytes, n, sizeof(uint));

                n += sizeof(uint);
            }

            GitId? sha = null;
            using (var fs = File.Create(Path.ChangeExtension(packFile, ".rev")))
            {
                await (Encoding.ASCII.GetBytes("RIDX").AsBucket()
                            + NetBitConverter.GetBytes(1 /* Version 1 */).AsBucket()
                            + NetBitConverter.GetBytes((int)idType).AsBucket()
                            + mapBytes.AsBucket()
                            + packChecksum.Hash.AsBucket())
                        .GitHash(idType, r => sha = r)
                    .WriteToAsync(fs, cancellationToken).ConfigureAwait(false);

                await fs.WriteAsync(sha!.Hash, cancellationToken).ConfigureAwait(false);
            }
        }

        return packChecksum;

        async Task<bool> IndexOne(long offset, Bucket src)
        {
            GitId? checksum = null;
            bool skipNoHash = false;

            GitObjectBucket FixUp(GitId id)
            {
                skipNoHash = true;
                return DummyObjectBucket.Instance;
            }

            Func<GitId, ValueTask<GitObjectBucket?>> idCallback = async id => await GetDeltaSource(id).ConfigureAwait(false) ?? FixUp(id);
            Func<long, ValueTask<GitObjectBucket>> ofsCallback = default!;
            ofsCallback = async itemOffset =>
            {
                var d = await SpecializedBucketExtensions.DuplicateSeekedAsync(srcFile, itemOffset).ConfigureAwait(false);

                return new BufferObjectBucket(new GitPackObjectBucket(d, idType, idCallback, ofsCallback));
            };

            var pf = new GitPackObjectBucket(src, idType, idCallback, ofsCallback);

            Debug.Assert(offset == src.Position);
            var type = await pf.ReadTypeAsync().ConfigureAwait(false);

            if (skipNoHash)
            {
                await pf.DisposeAsync();
                lock (hashes)
                {
                    fixUpLater.Add(offset);
                }

                return false;
            }
            else
            {
                var len = await pf.ReadRemainingBytesAsync().ConfigureAwait(false);

                await using var csum = (type.CreateHeader(len.Value) + pf).GitHash(idType, s => checksum = s);

                await csum.ReadSkipAsync(long.MaxValue).ConfigureAwait(false);

                lock (hashes)
                {
                    hashes.Add(checksum!, offset);
                }
                return true;
            }
        }
    }

    private sealed class DummyObjectBucket : GitObjectBucket
    {
        private DummyObjectBucket() : base(Empty)
        {
        }

        public static DummyObjectBucket Instance { get; } = new();

#pragma warning disable CA2215 // Dispose methods should call base class dispose
        protected override async ValueTask DisposeAsync(bool disposing)
#pragma warning restore CA2215 // Dispose methods should call base class dispose
        {
            //base.Dispose(disposing);
        }

        protected override bool AcceptDisposing()
        {
            return false;
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            return BucketBytes.Eof;
        }

        public override ValueTask<GitObjectType> ReadTypeAsync()
        {
            return new(GitObjectType.None);
        }

        public override ValueTask SeekAsync(long newPosition)
        {
            throw new NotSupportedException();
        }

        public override long? Position => 0;
    }

    private sealed class BufferObjectBucket : GitObjectBucket, IBucketSeek, IBucketPoll
    {
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly Bucket _buffer;
#pragma warning restore CA2213 // Disposable fields should be disposed
        public BufferObjectBucket(GitObjectBucket source)
            : base(source)
        {
            _buffer = Source.Buffer();
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            return _buffer.ReadAsync(requested);
        }

        public override BucketBytes Peek()
        {
            return _buffer.Peek();
        }

        public override ValueTask<GitObjectType> ReadTypeAsync()
        {
            return ((GitObjectBucket)Source).ReadTypeAsync();
        }

        public override long? Position => _buffer.Position;

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            return _buffer.ReadRemainingBytesAsync();
        }

        public override bool CanReset => _buffer.CanReset;

        public override void Reset()
        {
            _buffer.Reset();
        }

        public override ValueTask SeekAsync(long newPosition)
        {
            return _buffer.SeekAsync(newPosition);
        }

        public ValueTask<BucketBytes> PollAsync(int minRequested = 1)
        {
            return _buffer.PollAsync(minRequested);
        }
    }
}
