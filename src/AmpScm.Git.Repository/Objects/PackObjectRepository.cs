using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git.Objects
{
    internal sealed class PackObjectRepository : GitObjectRepository
    {
        readonly GitIdType _idType;
        FileBucket? _fIdx;
        FileBucket? _packBucket;
        FileBucket? _bitmapBucket;
        FileBucket? _revIdxBucket;
        int _ver;
        uint[]? _fanOut;
        bool _hasBitmap;
        int _bmpHdrSize;

        public PackObjectRepository(GitRepository repository, string packFile, GitIdType idType)
            : base(repository, "Pack:" + packFile)
        {
            PackFile = packFile ?? throw new ArgumentNullException(nameof(packFile));
            _idType = idType;
        }

        public string PackFile { get; }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    _fIdx?.Dispose();
                    _packBucket?.Dispose();
                    _bitmapBucket?.Dispose();
                    _revIdxBucket?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        async ValueTask InitAsync()
        {
            if (_ver == 0)
            {
                _fIdx ??= FileBucket.OpenRead(Path.ChangeExtension(PackFile, ".idx"));

                byte[] header = new byte[8];
                long fanOutOffset = -1;
                if (header.Length == await _fIdx.ReadAtAsync(0, header).ConfigureAwait(false))
                {
                    var index = new byte[] { 255, (byte)'t', (byte)'O', (byte)'c', 0, 0, 0, 2 };

                    if (header.SequenceEqual(index))
                    {
                        // We have a v2 header.
                        fanOutOffset = 8;
                        _ver = 2;
                    }
                    else if (header.Take(4).SequenceEqual(index.Take(4)))
                    {
                        // We have an unsupported future header
                        _ver = -1;
                        _fIdx.Dispose();
                        _fIdx = null;
                        return;
                    }
                    else
                    {
                        // We have a v0/v1 header, which is no header
                        fanOutOffset = 0;
                        _ver = 1;
                    }
                }

                if (_fanOut == null && _ver > 0)
                {
                    byte[] fanOut = new byte[4 * 256];

                    if (fanOut.Length == await _fIdx.ReadAtAsync(fanOutOffset, fanOut).ConfigureAwait(false))
                    {
                        _fanOut = new uint[256];
                        for (int i = 0; i < 256; i++)
                        {
                            _fanOut[i] = NetBitConverter.ToUInt32(fanOut, i * 4);
                        }
                    }

                    _hasBitmap = File.Exists(Path.ChangeExtension(PackFile, ".bitmap"));
                }
            }
        }

        private bool TryFindId(byte[] oids, GitId oid, out uint index)
        {
            int sz;

            if (oids.Length == 0)
            {
                index = 0;
                return false;
            }

            if (_ver == 2)
                sz = _idType.HashLength();
            else if (_ver == 1)
                sz = _idType.HashLength() + 4;
            else
            {
                index = 0;
                return false;
            }

            int first = 0, count = oids.Length / sz;
            int c = count;

            if (c == 0)
            {
                index = 0;
                return false;
            }

            while (first + 1 < c)
            {
                int mid = first + (c - first) / 2;

                var check = GitId.FromByteArrayOffset(_idType, oids, sz * mid);

                int n = oid.CompareTo(check);

                if (n == 0)
                {
                    index = (uint)mid;
                    return true;
                }
                else if (n < 0)
                    c = mid;
                else
                    first = mid + 1;
            }

            if (first >= count)
            {
                index = (uint)count;
                return false;
            }

            var check2 = GitId.FromByteArrayOffset(_idType, oids, sz * first);
            index = (uint)first;

            c = oid.CompareTo(check2);

            if (c == 0)
                return true;
            else if (c > 0)
                index++;

            return false;
        }

        private async ValueTask<byte[]> GetOidArrayAsync(uint start, uint count)
        {
            if (count == 0)
                return Array.Empty<byte>();

            if (_ver == 2)
            {
                int sz = GitId.HashLength(_idType);
                byte[] data = new byte[sz * count];

                long offset = 8 /* header */ + 256 * 4 /* fanout */ + sz * start;

                if (data.Length != await _fIdx!.ReadAtAsync(offset, data).ConfigureAwait(false))
                    return Array.Empty<byte>();

                return data;
            }
            else if (_ver == 1)
            {
                int sz = GitId.HashLength(_idType) + 4;
                byte[] data = new byte[sz * count];

                long offset = 256 * 4 /* fanout */ + sz * start;

                if (data.Length != await _fIdx!.ReadAtAsync(offset, data).ConfigureAwait(false))
                    return Array.Empty<byte>();

                return data;
            }
            else
                return Array.Empty<byte>();
        }

        private async ValueTask<byte[]> GetOffsetArrayAsync(uint start, uint count, byte[] oids)
        {
            if (count == 0)
                return Array.Empty<byte>();

            if (_ver == 2)
            {
                int sz = GitId.HashLength(_idType);
                byte[] data = new byte[4 * count];

                long offset = 8 /* header */ + 256 * 4 /* fanout */
                        + sz * _fanOut![255] // Hashes
                        + 4 * _fanOut[255] // Crc32
                        + 4 * start;

                if (data.Length != await _fIdx!.ReadAtAsync(offset, data).ConfigureAwait(false))
                    return Array.Empty<byte>();

                return data;
            }
            else if (_ver == 1)
            {
                // TODO: Data is interleaved
                return oids ?? throw new ArgumentNullException(nameof(oids));
            }
            else
                return Array.Empty<byte>();
        }

        private uint GetOffset(byte[] offsetArray, int index)
        {
            if (_ver == 2)
            {
                return NetBitConverter.ToUInt32(offsetArray, index * 4);
            }
            else if (_ver == 1)
            {
                // oidArray = offsetArray with chunks of [4-byte length, 20 or 32 byte oid]
                return NetBitConverter.ToUInt32(offsetArray, index * (4 + GitId.HashLength(_idType)));
            }
            else
                return uint.MaxValue;
        }

        private GitId GetOid(byte[] oidArray, int index)
        {
            if (_ver == 2)
            {
                int idBytes = GitId.HashLength(_idType);
                return GitId.FromByteArrayOffset(_idType, oidArray, index * idBytes);
            }
            else if (_ver == 1)
            {
                // oidArray = offsetArray with chunks of [4-byte length, 20 or 32 byte oid]
                int blockBytes = 4 + GitId.HashLength(_idType);
                return GitId.FromByteArrayOffset(_idType, oidArray, index * blockBytes + 4);
            }

            throw new GitRepositoryException("Unsupported pack version");
        }

        public override async ValueTask<TGitObject?> GetByIdAsync<TGitObject>(GitId id)
            where TGitObject : class
        {
            await InitAsync().ConfigureAwait(false);

            if (_fanOut is null)
                return null;

            byte byte0 = id[0];

            uint start = (byte0 == 0) ? 0 : _fanOut![byte0 - 1];
            uint count = _fanOut![byte0] - start;

            if (count == 0)
                return null;

            byte[] oids = await GetOidArrayAsync(start, count).ConfigureAwait(false);

            if (TryFindId(oids, id, out var index))
            {
                var r = await GetOffsetArrayAsync(index + start, 1, oids).ConfigureAwait(false);
                var offset = GetOffset(r, 0);

                return await GetByOffsetAsync<TGitObject>(offset, id).ConfigureAwait(false);
            }
            return null;
        }

        internal override bool ContainsId(GitId id)
        {
            InitAsync().AsTask().Wait();

            if (_fanOut is null)
                return false;

            byte byte0 = id[0];

            uint start = (byte0 == 0) ? 0 : _fanOut![byte0 - 1];
            uint count = _fanOut![byte0] - start;

            if (count == 0)
                return false;

            byte[] oids = GetOidArrayAsync(start, count).AsTask().Result;

            return TryFindId(oids, id, out var _);
        }

        internal async ValueTask<TGitObject?> GetByOffsetAsync<TGitObject>(long offset, GitId id)
            where TGitObject : GitObject
        {
            await OpenPackIfNecessary().ConfigureAwait(false);

            var rdr = _packBucket!.Duplicate(true);
            await rdr.SeekAsync(offset).ConfigureAwait(false);

            GitPackObjectBucket pf = new GitPackObjectBucket(rdr, _idType, MyResolveByOid);

            GitObject ob = await GitObject.FromBucketAsync(Repository, pf, id).ConfigureAwait(false);

            if (ob is TGitObject tg)
                return tg;
            else
                pf.Dispose();
            return null;
        }

        internal override async ValueTask<(TGitObject? Result, bool Success)> DoResolveIdString<TGitObject>(string idString, GitId baseGitId)
            where TGitObject : class
        {
            await InitAsync().ConfigureAwait(false);

            if (_fanOut is null)
                return (null, true);

            byte byte0 = baseGitId[0];

            uint start = (byte0 == 0) ? 0 : _fanOut![byte0 - 1];
            uint count = _fanOut![byte0] - start;

            if (count == 0)
                return (null, true);

            byte[] oids = await GetOidArrayAsync(start, count).ConfigureAwait(false);

            if (TryFindId(oids, baseGitId, out var index) || (index >= 0 && index < count))
            {
                GitId foundId = GetOid(oids, (int)index);

                if (!foundId.ToString().StartsWith(idString, StringComparison.OrdinalIgnoreCase))
                    return (null, true); // Not a match, but success


                if (index + 1 < count)
                {
                    GitId next = GetOid(oids, (int)index + 1);

                    if (next.ToString().StartsWith(idString, StringComparison.OrdinalIgnoreCase))
                    {
                        // We don't have a single match. Return failure

                        return (null, false);
                    }
                }

                var r = await GetOffsetArrayAsync(index + start, 1, oids).ConfigureAwait(false);
                var offset = GetOffset(r, 0);

                await OpenPackIfNecessary().ConfigureAwait(false);

                var rdr = _packBucket!.Duplicate(true);
                await rdr.SeekAsync(offset).ConfigureAwait(false);

                GitPackObjectBucket pf = new GitPackObjectBucket(rdr, _idType, MyResolveByOid);

                GitObject ob = await GitObject.FromBucketAsync(Repository, pf, foundId).ConfigureAwait(false);

                if (ob is TGitObject tg)
                    return (tg, true); // Success
                else
                    pf.Dispose();

                return (null, false); // We had a match. No singular good result
            }
            else
                return (null, true);
        }


        private async ValueTask OpenPackIfNecessary()
        {
            if (_packBucket == null)
            {
                var fb = new FileBucket(PackFile, chunkSize: 2048);

                await VerifyPack(fb).ConfigureAwait(false);

                _packBucket = fb;
            }
        }

        private async ValueTask VerifyPack(FileBucket fb)
        {
            using var phr = new GitPackHeaderBucket(fb.NoDispose());

            var bb = await phr.ReadAsync().ConfigureAwait(false);

            if (!bb.IsEof)
                throw new GitBucketException("Error during reading of pack header");
            else if (phr.GitType != "PACK")
                throw new GitBucketException($"Error during reading of pack header, type='{phr.GitType}");
            else if (phr.Version != 2)
                throw new GitBucketException($"Unexpected pack version '{phr.Version}, expected version 2");
            else if (_fanOut != null && phr.ObjectCount != _fanOut[255])
                throw new GitBucketException($"Header has {phr.ObjectCount} records, index {_fanOut[255]}, for {Path.GetFileName(PackFile)}");
        }

        public override IAsyncEnumerable<TGitObject> GetAll<TGitObject>(HashSet<GitId> alreadyReturned)
            where TGitObject : class
        {
#pragma warning disable CA1849 // Call async methods when in an async method
            InitAsync().AsTask().Wait();
#pragma warning restore CA1849 // Call async methods when in an async method

            if (typeof(TGitObject) != typeof(GitObject) && _hasBitmap)
            {
                return GetAllViaBitmap<TGitObject>(alreadyReturned);
            }
            else
            {
                return GetAllAll<TGitObject>(alreadyReturned);
            }
        }


        async IAsyncEnumerable<TGitObject> GetAllAll<TGitObject>(HashSet<GitId> alreadyReturned)
            where TGitObject : class
        {
            await OpenPackIfNecessary().ConfigureAwait(false);

            if (_fanOut is null || _fanOut[255] == 0)
                yield break;

            uint count = _fanOut[255];

            byte[] oids = await GetOidArrayAsync(0, count).ConfigureAwait(false);
            byte[] offsets = await GetOffsetArrayAsync(0, count, oids).ConfigureAwait(false);

            for (int i = 0; i < count; i++)
            {
                GitId objectId = GetOid(oids, i);

                if (alreadyReturned.Contains(objectId))
                    continue;

                long offset = GetOffset(offsets, i);

                var rdr = _packBucket!.Duplicate(true);
                await rdr.SeekAsync(offset).ConfigureAwait(false);

                GitPackObjectBucket pf = new GitPackObjectBucket(rdr, _idType, MyResolveByOid);

                GitObject ob = await GitObject.FromBucketAsync(Repository, pf, objectId).ConfigureAwait(false);

                if (ob is TGitObject one)
                    yield return one;
                else
                    pf.Dispose();
            }
        }

        async IAsyncEnumerable<TGitObject> GetAllViaBitmap<TGitObject>(HashSet<GitId> alreadyReturned)
            where TGitObject : GitObject
        {
            await OpenPackIfNecessary().ConfigureAwait(false);

            if (_fanOut is null || _fanOut[255] == 0)
                yield break;

            if (_bitmapBucket == null)
            {
                var bmp = FileBucket.OpenRead(Path.ChangeExtension(PackFile, ".bitmap"));

                await VerifyBitmap(bmp).ConfigureAwait(false);
                _bmpHdrSize = (int)bmp.Position!.Value;
                _bitmapBucket = bmp;

            }
            await _bitmapBucket.SeekAsync(_bmpHdrSize).ConfigureAwait(false);

            GitEwahBitmapBucket? ewahBitmap = null;

            // This is how the bitmaps are ordered in a V1 bitmap file
            foreach (Type tp in new Type[] { typeof(GitCommit), typeof(GitTree), typeof(GitBlob), typeof(GitTagObject) })
            {
                var ew = new GitEwahBitmapBucket(_bitmapBucket);

                if (tp == typeof(TGitObject))
                {
                    ewahBitmap = ew;
                    break;
                }
                else
                {
                    await ew.ReadUntilEofAsync().ConfigureAwait(false);
                }
            }

            if (ewahBitmap == null)
                throw new InvalidOperationException();

            GitObjectType gitObjectType = GetGitObjectType(typeof(TGitObject));

            await foreach (int index in ewahBitmap.SetIndexes)
            {
                var v = await GetOneViaPackIndex<TGitObject>(index, gitObjectType, alreadyReturned.Contains).ConfigureAwait(false);

                if (v is not null)
                    yield return v;
            }
        }

        async ValueTask<TGitObject?> GetOneViaPackIndex<TGitObject>(int v, GitObjectType gitObjectType, Predicate<GitId> skip)
            where TGitObject : class
        {
            await OpenPackIfNecessary().ConfigureAwait(false);

            if (!File.Exists(Path.ChangeExtension(PackFile, ".rev")))
            {
                await CreateReverseIndex().ConfigureAwait(false);
            }

            _revIdxBucket ??= FileBucket.OpenRead(Path.ChangeExtension(PackFile, ".rev"));
            _revIdxBucket.Reset();
            await _revIdxBucket.ReadSkipAsync(12 + sizeof(uint) * v).ConfigureAwait(false);
            var indexOffs = await _revIdxBucket.ReadNetworkUInt32Async().ConfigureAwait(false);

            byte[] oids = await GetOidArrayAsync(indexOffs, 1).ConfigureAwait(false);
            byte[] offsets = await GetOffsetArrayAsync(indexOffs, 1, oids).ConfigureAwait(false);

            GitId objectId = GitId.FromByteArrayOffset(_idType, oids, 0);

            if (skip(objectId))
                return null;

            var rdr = _packBucket!.Duplicate(true);
            await rdr.SeekAsync(GetOffset(offsets, 0)).ConfigureAwait(false);

            GitPackObjectBucket pf = new GitPackObjectBucket(rdr, _idType, MyResolveByOid);

            return (TGitObject)(object)await GitObject.FromBucketAsync(Repository, pf, objectId, gitObjectType).ConfigureAwait(false);
        }

        private async ValueTask CreateReverseIndex()
        {
            await InitAsync().ConfigureAwait(false);

            if (_fanOut == null)
                return;

            var revName = Path.ChangeExtension(PackFile, ".rev")!;
            var tmpName = revName + ".t" + Guid.NewGuid();

            // TODO: Use less memory
            byte[] mapBytes;
            {
                SortedList<long, int> map = new SortedList<long, int>((int)_fanOut[255]);

                uint count = _fanOut[255];

                byte[]? oids;
                if (_ver == 1)
                    oids = await GetOidArrayAsync(0, count).ConfigureAwait(false);
                else
                    oids = null;
                byte[] offsets = await GetOffsetArrayAsync(0, count, oids!).ConfigureAwait(false);

                int n = 0;
                for (uint i = 0; i < count; i++)
                {
                    long offset = GetOffset(offsets, (int)i);

                    map[offset] = n++;
                }

                mapBytes = new byte[sizeof(uint) * n];
                n = 0;
                foreach (var v in map.Values)
                {
                    Array.Copy(NetBitConverter.GetBytes(v), 0, mapBytes, n, sizeof(uint));

                    n += sizeof(uint);
                }
            }

            GitId? sha = null;
            using (var fs = File.Create(tmpName))
            {
                await (Encoding.ASCII.GetBytes("RIDX").AsBucket()
                            + NetBitConverter.GetBytes((int)1 /* Version 1 */).AsBucket()
                            + NetBitConverter.GetBytes((int)_idType).AsBucket()
                            + mapBytes.AsBucket()
                            + GitId.Parse(Path.GetFileNameWithoutExtension(PackFile).Substring(5)).Hash.AsBucket())
                        .GitHash(Repository.InternalConfig.IdType, r => sha = r)
                    .WriteToAsync(fs).ConfigureAwait(false);

                await fs.WriteAsync(sha!.Hash).ConfigureAwait(false);
            }

            if (!File.Exists(revName))
                File.Move(tmpName, revName);
            else
                File.Delete(tmpName);
        }

        async ValueTask VerifyBitmap(FileBucket bmp)
        {
            using var bhr = new GitBitmapHeaderBucket(bmp.NoDispose(), Repository.InternalConfig.IdType);

            var bb = await bhr.ReadAsync().ConfigureAwait(false);

            if (!bb.IsEof)
                throw new GitBucketException("Error during reading of pack header");
            else if (bhr.BitmapType != "BITM")
                throw new GitBucketException($"Error during reading of pack header, type='{bhr.BitmapType}");
            else if (bhr.Version != 1)
                throw new GitBucketException($"Unexpected bitmap version '{bhr.Version}, expected version 1");
            else if ((bhr.Flags & 1) != 1)
                throw new GitBucketException($"BITMAP_OPT_FULL_DAG not set, flags={bhr.Flags}");
        }

        async ValueTask<GitObjectBucket?> MyResolveByOid(GitId id)
        {
            // 99% case. All deltas should be in the same pack
            var r = await ResolveById(id).ConfigureAwait(false);
            if (r is not null)
                return r;

            return (await Repository.ObjectRepository.ResolveById(id).ConfigureAwait(false)) ?? throw new GitRepositoryException($"Unexpected unresolvable reference to id {id}");
        }

        internal override async ValueTask<GitObjectBucket?> ResolveById(GitId id)
        {
            await InitAsync().ConfigureAwait(false);

            if (_fanOut is null)
                return null!;

            byte byte0 = id[0];

            uint start = (byte0 == 0) ? 0 : _fanOut![byte0 - 1];
            uint count = _fanOut![byte0] - start;

            if (count == 0)
                return null;

            byte[] oids = await GetOidArrayAsync(start, count).ConfigureAwait(false);

            if (TryFindId(oids, id, out var index))
            {
                var r = await GetOffsetArrayAsync(index + start, 1, oids).ConfigureAwait(false);
                var offset = GetOffset(r, 0);

                await OpenPackIfNecessary().ConfigureAwait(false);

                var rdr = _packBucket!.Duplicate(true);
                await rdr.SeekAsync(offset).ConfigureAwait(false);

                GitPackObjectBucket pf = new GitPackObjectBucket(rdr, _idType, MyResolveByOid);

                return pf;
            }
            return null;
        }

        internal override bool ProvidesCommitInfo => false;

        public override long ObjectCount
        {
            get
            {
                if (_fanOut is null)
                    InitAsync().AsTask().Wait();

                return (int)(_fanOut?[255] ?? 0);
            }
        }
    }
}
