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
        readonly string _packFile;
        readonly GitIdType _idType;
        FileStream? _fIdx;
        FileBucket? _packBucket;
        FileBucket? _bitmapBucket;
        FileBucket? _revIdxBucket;
        int _ver;
        uint[]? _fanOut;
        bool _hasBitmap;

        public PackObjectRepository(GitRepository repository, string packFile, GitIdType idType)
            : base(repository, "Pack:" + packFile)
        {
            _packFile = packFile ?? throw new ArgumentNullException(nameof(packFile));
            _idType = idType;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    _packBucket?.Dispose();
                    _bitmapBucket?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        void Init()
        {
            if (_ver == 0)
            {
                _fIdx ??= File.OpenRead(Path.ChangeExtension(_packFile, ".idx"));

                byte[] header = new byte[8];
                long fanOutOffset = -1;
                if (8 == _fIdx.Read(header, 0, header.Length))
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

                    _fIdx.Position = fanOutOffset;

                    if (fanOut.Length == _fIdx.Read(fanOut, 0, fanOut.Length))
                    {
                        _fanOut = new uint[256];
                        for (int i = 0; i < 256; i++)
                        {
                            _fanOut[i] = NetBitConverter.ToUInt32(fanOut, i * 4);
                        }
                    }

                    _hasBitmap = File.Exists(Path.ChangeExtension(_packFile, ".bitmap"));
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
                sz = GitId.HashLength(_idType);
            else if (_ver == 1)
                sz = GitId.HashLength(_idType) + 4;
            else
            {
                index = 0;
                return false;
            }

            int first = 0, count = oids.Length / sz;
            int c = count;

            while (first < c - 1)
            {
                int mid = (first + c) / 2;

                var check = GitId.FromByteArrayOffset(_idType, oids, sz * mid);

                int n = oid.HashCompare(check);

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

            c = oid.HashCompare(check2);

            if (c == 0)
                return true;
            else if (c > 0)
                index++;

            return false;
        }

        private byte[] GetOidArray(uint start, uint count)
        {
            if (_ver == 2)
            {
                int sz = GitId.HashLength(_idType);
                byte[] data = new byte[sz * count];

                _fIdx!.Position = 8 /* header */ + 256 * 4 /* fanout */ + sz * start;

                if (data.Length != _fIdx.Read(data, 0, data.Length))
                    return Array.Empty<byte>();

                return data;
            }
            else if (_ver == 1)
            {
                int sz = GitId.HashLength(_idType) + 4;
                byte[] data = new byte[sz * count];

                _fIdx!.Position = 256 * 4 /* fanout */ + sz * start;

                if (data.Length != _fIdx.Read(data, 0, data.Length))
                    return Array.Empty<byte>();

                return data;
            }
            else
                return Array.Empty<byte>();
        }

        private byte[] GetOffsetArray(uint start, uint count, byte[] oids)
        {
            if (_ver == 2)
            {
                int sz = GitId.HashLength(_idType);
                byte[] data = new byte[4 * count];

                _fIdx!.Position = 8 /* header */ + 256 * 4 /* fanout */
                        + sz * _fanOut![255] // Hashes
                        + 4 * _fanOut[255] // Crc32
                        + 4 * start;

                if (data.Length != _fIdx.Read(data, 0, data.Length))
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
            Init();

            if (_fanOut is null)
                return null;

            byte byte0 = id[0];

            uint start = (byte0 == 0) ? 0 : _fanOut![byte0 - 1];
            uint count = _fanOut![byte0] - start;

            byte[] oids = GetOidArray(start, count);

            if (TryFindId(oids, id, out var index))
            {
                var r = GetOffsetArray(index + start, 1, oids);
                var offset = GetOffset(r, 0);

                return await GetByOffsetAsync<TGitObject>(offset, id).ConfigureAwait(false);
            }
            return null;
        }

        internal override bool ContainsId(GitId id)
        {
            Init();

            if (_fanOut is null)
                return false;

            byte byte0 = id[0];

            uint start = (byte0 == 0) ? 0 : _fanOut![byte0 - 1];
            uint count = _fanOut![byte0] - start;

            byte[] oids = GetOidArray(start, count);

            return TryFindId(oids, id, out var _);
        }

        internal async ValueTask<TGitObject?> GetByOffsetAsync<TGitObject>(long offset, GitId id)
            where TGitObject : GitObject
        {
            await OpenPackIfNecessary().ConfigureAwait(false);

            var rdr = await _packBucket!.DuplicateAsync(true).ConfigureAwait(false);
            await rdr.ReadSkipAsync(offset).ConfigureAwait(false);

            GitPackFrameBucket pf = new GitPackFrameBucket(rdr, _idType, MyResolveByOid);

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
            Init();

            if (_fanOut is null)
                return (null, true);

            byte byte0 = baseGitId[0];

            uint start = (byte0 == 0) ? 0 : _fanOut![byte0 - 1];
            uint count = _fanOut![byte0] - start;

            byte[] oids = GetOidArray(start, count);

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

                var r = GetOffsetArray(index + start, 1, oids);
                var offset = GetOffset(r, 0);

                await OpenPackIfNecessary().ConfigureAwait(false);

                var rdr = await _packBucket!.DuplicateAsync(true).ConfigureAwait(false);
                await rdr.ReadSkipAsync(offset).ConfigureAwait(false);

                GitPackFrameBucket pf = new GitPackFrameBucket(rdr, _idType, MyResolveByOid);

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


        private async Task OpenPackIfNecessary()
        {
            if (_packBucket == null)
            {
                var fb = FileBucket.OpenRead(_packFile);

                await VerifyPack(fb).ConfigureAwait(false);

                _packBucket = fb;
            }
        }

        private async ValueTask VerifyPack(FileBucket fb)
        {
            using var phr = new GitPackHeaderBucket(fb.NoClose());

            var bb = await phr.ReadAsync().ConfigureAwait(false);

            if (!bb.IsEof)
                throw new GitBucketException("Error during reading of pack header");
            else if (phr.GitType != "PACK")
                throw new GitBucketException($"Error during reading of pack header, type='{phr.GitType}");
            else if (phr.Version != 2)
                throw new GitBucketException($"Unexpected pack version '{phr.Version}, expected version 2");
            else if (_fanOut != null && phr.ObjectCount != _fanOut[255])
                throw new GitBucketException($"Header has {phr.ObjectCount} records, index {_fanOut[255]}, for {Path.GetFileName(_packFile)}");
        }

        public override IAsyncEnumerable<TGitObject> GetAll<TGitObject>(HashSet<GitId> alreadyReturned)
            where TGitObject : class
        {
            Init();

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

            byte[] oids = GetOidArray(0, count);
            byte[] offsets = GetOffsetArray(0, count, oids);

            for (int i = 0; i < count; i++)
            {
                GitId objectId = GetOid(oids, i);

                if (alreadyReturned.Contains(objectId))
                    continue;

                long offset = GetOffset(offsets, i);

                var rdr = await _packBucket!.DuplicateAsync(true).ConfigureAwait(false);
                await rdr.ReadSkipAsync(offset).ConfigureAwait(false);

                GitPackFrameBucket pf = new GitPackFrameBucket(rdr, _idType, MyResolveByOid);

                GitObject ob = await GitObject.FromBucketAsync(Repository, pf, objectId).ConfigureAwait(false);

                if (ob is TGitObject one)
                    yield return one;
                else
                    pf.Dispose();
            }
        }

        async IAsyncEnumerable<TGitObject> GetAllViaBitmap<TGitObject>(HashSet<GitId> alreadyReturned)
            where TGitObject : class
        {
            await OpenPackIfNecessary().ConfigureAwait(false);

            if (_fanOut is null || _fanOut[255] == 0)
                yield break;

            if (_bitmapBucket == null)
            {
                var bmp = FileBucket.OpenRead(Path.ChangeExtension(_packFile, ".bitmap"));

                await VerifyBitmap(bmp).ConfigureAwait(false);
                _bitmapBucket = bmp;
            }
            await _bitmapBucket.ResetAsync().ConfigureAwait(false);
            await _bitmapBucket.ReadSkipAsync(32).ConfigureAwait(false);

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
                    await ew.ReadSkipUntilEofAsync().ConfigureAwait(false);
                }
            }

            if (ewahBitmap == null)
                throw new InvalidOperationException();

            GitObjectType gitObjectType = GetGitObjectType(typeof(TGitObject));
            int bit = 0;
            int? bitLength = null;
            while (await ewahBitmap.ReadByteAsync().ConfigureAwait(false) is byte b)
            {
                if (b != 0)
                {
                    for (int n = 0; n < 8; n++)
                    {
                        if ((b & (1 << n)) != 0)
                        {
                            if (bit + n < (bitLength ??= await ewahBitmap.ReadBitLengthAsync().ConfigureAwait(false)))
                            {
                                yield return await GetOneViaPackOffset<TGitObject>(bit + n, gitObjectType).ConfigureAwait(false);
                            }
                        }
                    }
                }
                bit += 8;
            }
        }

        static GitObjectType GetGitObjectType(Type type)
        {
            if (type == typeof(GitCommit))
                return GitObjectType.Commit;
            else if (type == typeof(GitTree))
                return GitObjectType.Tree;
            else if (type == typeof(GitBlob))
                return GitObjectType.Blob;
            else if (type == typeof(GitTagObject))
                return GitObjectType.Tag;
            else
                throw new InvalidOperationException();
        }

        private async ValueTask<TGitObject> GetOneViaPackOffset<TGitObject>(int v, GitObjectType gitObjectType)
            where TGitObject : class
        {
            if (!File.Exists(Path.ChangeExtension(_packFile, ".rev")))
            {
                await CreateReverseIndex();
            }

            _revIdxBucket ??= FileBucket.OpenRead(Path.ChangeExtension(_packFile, ".rev"));
            await _revIdxBucket.ResetAsync().ConfigureAwait(false);
            await _revIdxBucket.ReadSkipAsync(12 + sizeof(uint) * v).ConfigureAwait(false);
            var indexOffs = await _revIdxBucket.ReadNetworkUInt32Async().ConfigureAwait(false);

            byte[] oids = GetOidArray(indexOffs, 1);
            byte[] offsets = GetOffsetArray(indexOffs, 1, oids);

            GitId objectId = GitId.FromByteArrayOffset(_idType, oids, 0);

            var rdr = await _packBucket!.DuplicateAsync(true).ConfigureAwait(false);
            await rdr.ReadSkipAsync(GetOffset(offsets, 0)).ConfigureAwait(false);

            GitPackFrameBucket pf = new GitPackFrameBucket(rdr, _idType, MyResolveByOid);

            return (TGitObject)(object)await GitObject.FromBucketAsync(Repository, pf, objectId, gitObjectType).ConfigureAwait(false);
        }

        private async ValueTask CreateReverseIndex()
        {
            Init();

            if (_fanOut == null)
                return;

            var bmpName = Path.ChangeExtension(_packFile, ".rev");
            var tmpName = bmpName + ".t" + Guid.NewGuid();

            // TODO: Use less memory
            byte[] mapBytes;
            {
                SortedList<long, int> map = new SortedList<long, int>((int)_fanOut[255]);

                uint count = _fanOut[255];

                byte[]? oids = GetOidArray(0, count);
                if (_ver == 1)
                    oids = GetOidArray(0, count);
                else
                    oids = null;
                byte[] offsets = GetOffsetArray(0, count, oids!);

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

            byte[]? sha = null;
            using (Bucket b = (Encoding.ASCII.GetBytes("RIDX").AsBucket()
                                + NetBitConverter.GetBytes((int)1 /* Version 1 */).AsBucket()
                                + NetBitConverter.GetBytes((int)_idType).AsBucket()
                                + mapBytes.AsBucket()
                                + GitId.Parse(Path.GetFileNameWithoutExtension(_packFile).Substring(5)).Hash.AsBucket())
                            .SHA1(r => sha = r))
            using (var fs = File.Create(tmpName))
            {
                await fs.WriteAsync(b).ConfigureAwait(false);
            }

            if (!File.Exists(bmpName))
                File.Move(tmpName, bmpName);
            else
                File.Delete(tmpName);
        }

        private async ValueTask VerifyBitmap(FileBucket bmp)
        {
            using var bhr = new GitBitmapHeaderBucket(bmp.NoClose());

            var bb = await bhr.ReadAsync().ConfigureAwait(false);

            if (!bb.IsEof)
                throw new GitBucketException("Error during reading of pack header");
            else if (bhr.BitmapType != "BITM")
                throw new GitBucketException($"Error during reading of pack header, type='{bhr.BitmapType}");
            else if (bhr.Version != 1)
                throw new GitBucketException($"Unexpected bitmap version '{bhr.Version}, expected version 1");
            else if (_fanOut != null && bhr.ObjectCount > _fanOut[255])
                throw new GitBucketException($"Bitmap Header has {bhr.ObjectCount} commit records, index {_fanOut[255]}, for {Path.GetFileName(_packFile)}");
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
            Init();

            if (_fanOut is null)
                return null!;

            byte byte0 = id[0];

            uint start = (byte0 == 0) ? 0 : _fanOut![byte0 - 1];
            uint count = _fanOut![byte0] - start;

            byte[] oids = GetOidArray(start, count);

            if (TryFindId(oids, id, out var index))
            {
                var r = GetOffsetArray(index + start, 1, oids);
                var offset = GetOffset(r, 0);

                await OpenPackIfNecessary().ConfigureAwait(false);

                var rdr = await _packBucket!.DuplicateAsync(true).ConfigureAwait(false);
                await rdr.ReadSkipAsync(offset).ConfigureAwait(false);

                GitPackFrameBucket pf = new GitPackFrameBucket(rdr, _idType, MyResolveByOid);

                return pf;
            }
            return null!;
        }

        internal override bool ProvidesCommitInfo => false;
    }
}
