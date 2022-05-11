using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Repository.Implementation;

namespace AmpScm.Git.Objects
{
    internal class MultiPackObjectRepository : ChunkFileBasedObjectRepository
    {
        readonly string _dir;
        private string[]? _packNames;
        PackObjectRepository[]? _packs;
        string? _multiPackBitmapPath;
        Lazy<bool> HasBitmap;
        FileBucket? _bitmapBucket;
        FileBucket? _revIdxBucket;

        public MultiPackObjectRepository(GitRepository repository, string multipackFile) : base(repository, multipackFile, "MultiPack:" + repository.GitDir)
        {
            _dir = Path.GetDirectoryName(multipackFile)!;
            HasBitmap = new GitAsyncLazy<bool>(GetHasBitmap);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_packs != null)
                    {
                        foreach (var p in _packs)
                            p.Dispose();
                    }
                    _bitmapBucket?.Dispose();
                    _revIdxBucket?.Dispose();
                }
            }
            finally
            {
                _packs = null;
                base.Dispose(disposing);
            }
        }

        async ValueTask<bool> GetHasBitmap()
        {
            if (ChunkReader is null)
                return false;

            byte[] multiPackId = new byte[Repository.InternalConfig.IdType.HashLength()];
            if (multiPackId.Length != (await ChunkReader.ReadAtAsync(AfterChunkPosition, multiPackId).ConfigureAwait(false)))
                return false;

            GitId id = new GitId(Repository.InternalConfig.IdType, multiPackId);

            _multiPackBitmapPath = Path.Combine(_dir, $"multi-pack-index-{id}.bitmap");
            return File.Exists(_multiPackBitmapPath);
        }

        public override long ObjectCount => FanOut is not null ? FanOut[255] : 0;

        public override async ValueTask<TGitObject?> GetByIdAsync<TGitObject>(GitId id)
            where TGitObject : class
        {
            var (success, index) = await TryFindIdAsync(id).ConfigureAwait(false);
            if (success)
            {
                return await GetByIndexAsync<TGitObject>(index, id).ConfigureAwait(false);
            }
            return null;
        }

        private async ValueTask<TGitObject?> GetByIndexAsync<TGitObject>(uint index, GitId id)
            where TGitObject : GitObject
        {
            if (_packs == null)
                return null; // Not really loaded yet

            var result = new byte[2 * sizeof(uint)];
            if (await ReadFromChunkAsync("OOFF", index * result.Length, result).ConfigureAwait(false) == result.Length)
            {
                int pack = NetBitConverter.ToInt32(result, 0);
                long offset = NetBitConverter.ToUInt32(result, 4);

                if (offset > int.MaxValue && GetChunkLength("LOFF") != null) // If chunk does not exist we have 32 bits
                {
                    throw new NotImplementedException("TODO: Implement LOFF support on MIDX");
                }

                if (pack < _packs.Length)
                {
                    return await _packs[pack].GetByOffsetAsync<TGitObject>(offset, id).ConfigureAwait(false);
                }
            }

            return null;
        }

        internal async override ValueTask<GitObjectBucket?> ResolveById(GitId id)
        {
            if (_packs == null)
                return null; // Not really loaded yet

            // TODO: Find in multipack and directly open via index
            foreach (var p in _packs)
            {
                var r = await p.ResolveById(id).ConfigureAwait(false);

                if (r is not null)
                    return r;
            }
            return null;
        }

        internal override async ValueTask<(TGitObject? Result, bool Success)> DoResolveIdString<TGitObject>(string idString, GitId baseGitId)
            where TGitObject : class
        {
            if (_packs == null)
                return (null, true); // Not really loaded yet

            if (FanOut == null)
                await InitAsync().ConfigureAwait(false);

            uint count = FanOut![255];

            var (success, index) = await TryFindIdAsync(baseGitId).ConfigureAwait(false);
            if (success || (index >= 0 && index < count))
            {
                GitId foundId = await GetGitIdByIndexAsync(index).ConfigureAwait(false);

                if (!foundId.ToString().StartsWith(idString, StringComparison.OrdinalIgnoreCase))
                    return (null, true); // Not a match, but success


                if (index + 1 < count)
                {
                    GitId next = await GetGitIdByIndexAsync(index + 1).ConfigureAwait(false);

                    if (next.ToString().StartsWith(idString, StringComparison.OrdinalIgnoreCase))
                    {
                        // We don't have a single match. Return failure

                        return (null, false);
                    }
                }

                return (await GetByIndexAsync<TGitObject>(index, foundId).ConfigureAwait(false), true);
            }

            return (null, true);
        }

        internal override bool ContainsId(GitId id)
        {
            if (_packs == null)
                return false; // Not really loaded yet

            if (FanOut == null)
                InitAsync().AsTask().Wait();

            return TryFindIdAsync(id).AsTask().Result.Success;
        }

        public override async IAsyncEnumerable<TGitObject> GetAll<TGitObject>(HashSet<GitId> alreadyReturned)
        {
            if (_packs == null)
                yield break; // Not really loaded yet

            if (typeof(TGitObject) != typeof(GitObject) && HasBitmap.Value)
            {
                await foreach (var x in GetAllViaBitmap<TGitObject>(alreadyReturned))
                {
                    yield return x;
                }
            }
            else
            {
                // Prefer locality of packs, over the multipack order when not using bitmaps
                foreach (var p in _packs)
                {
                    await foreach (var x in p.GetAll<TGitObject>(alreadyReturned))
                    {
                        yield return x;
                    }
                }
            }
        }

        async IAsyncEnumerable<TGitObject> GetAllViaBitmap<TGitObject>(HashSet<GitId> alreadyReturned)
            where TGitObject : GitObject
        {
            if (FanOut is null || FanOut[255] == 0 || !HasBitmap.Value)
            {
                await InitAsync().ConfigureAwait(false);

                if (FanOut is null || FanOut[255] == 0)
                    yield break;
            }

            if (_bitmapBucket == null)
            {
                var bmp = FileBucket.OpenRead(_multiPackBitmapPath!);

                await VerifyBitmap(bmp).ConfigureAwait(false);
                _bitmapBucket = bmp;
            }
            await _bitmapBucket.SeekAsync(32).ConfigureAwait(false);

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

            await foreach (int index in ewahBitmap.SetIndexes)
            {
                yield return await GetOneViaMultiPackOffset<TGitObject>(index, gitObjectType).ConfigureAwait(false);
            }
        }

        private async ValueTask<TGitObject> GetOneViaMultiPackOffset<TGitObject>(int v, GitObjectType gitObjectType)
            where TGitObject : GitObject
        {
            if (base.GetChunkLength("RIDX") > 0)
            {
                byte[] indexBuffer = new byte[sizeof(uint)];
                if (await base.ReadFromChunkAsync("RIDX", v * sizeof(uint), indexBuffer).ConfigureAwait(false) == sizeof(uint))
                {
                    var idx = NetBitConverter.ToUInt32(indexBuffer, 0);

                    GitId oid = await GetGitIdByIndexAsync(idx).ConfigureAwait(false);
                    return await GetByIndexAsync<TGitObject>(idx, oid).ConfigureAwait(false) ?? throw new GitException($"Expected {oid} to be {gitObjectType} via multipack index {v}");
                }

                throw new GitException($"Multipack reverse index {v} out of range");
            }
            else
            {
                if (_revIdxBucket is null && !File.Exists(Path.ChangeExtension(_multiPackBitmapPath, ".rev")))
                {
                    await CreateReverseIndex().ConfigureAwait(false);
                }

                _revIdxBucket ??= FileBucket.OpenRead(Path.ChangeExtension(_multiPackBitmapPath!, ".rev"));
                await _revIdxBucket.ResetAsync().ConfigureAwait(false);
                await _revIdxBucket.ReadSkipAsync(12 + sizeof(uint) * v).ConfigureAwait(false);

                var index = await _revIdxBucket.ReadNetworkUInt32Async().ConfigureAwait(false);
                GitId id = await GetGitIdByIndexAsync(index).ConfigureAwait(false);

                return await GetByIndexAsync<TGitObject>(index, id).ConfigureAwait(false) ?? throw new GitException("");
            }
        }

        private async ValueTask CreateReverseIndex()
        {
            if (FanOut == null)
                return;

            var revName = Path.ChangeExtension(_multiPackBitmapPath, ".rev")!;
            var tmpName = revName + ".t" + Guid.NewGuid();

            // TODO: Use less memory
            byte[] mapBytes;
            {
                SortedList<long, int> map = new SortedList<long, int>((int)FanOut[255]);

                uint count = FanOut[255];
                byte[] offsets = new byte[count * 2 * sizeof(uint)];

                if (await ReadFromChunkAsync("OOFF", 0, offsets).ConfigureAwait(false) != offsets.Length)
                    throw new InvalidOperationException();

                int n = 0;
                for (int i = 0; i < count; i++)
                {
                    int pack = NetBitConverter.ToInt32(offsets, 0 + 2 * sizeof(uint) * i);
                    long offset = NetBitConverter.ToUInt32(offsets, 4 + 2 * sizeof(uint) * i);

                    if (offset > int.MaxValue && GetChunkLength("LOFF") != null) // If chunk doesn't exist we have 32 bits
                    {
                        throw new NotImplementedException("TODO: Implement LOFF support on MIDX");
                    }

                    offset += ((long)pack) << 48;

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
            using (Bucket b = (Encoding.ASCII.GetBytes("RIDX").AsBucket()
                                + NetBitConverter.GetBytes((int)1 /* Version 1 */).AsBucket()
                                + NetBitConverter.GetBytes((int)Repository.InternalConfig.IdType).AsBucket()
                                + mapBytes.AsBucket()
                                + GitId.Parse(Path.GetFileNameWithoutExtension(_multiPackBitmapPath!).Substring("multi-pack-index-".Length)).Hash.AsBucket())
                            .GitHash(Repository.InternalConfig.IdType, r => sha = r))
            using (var fs = File.Create(tmpName))
            {
                await b.WriteToAsync(fs).ConfigureAwait(false);
                await fs.WriteAsync(sha!.Hash).ConfigureAwait(false);
            }

            if (!File.Exists(revName))
                File.Move(tmpName, revName);
            else
                File.Delete(tmpName);
        }

        static async ValueTask VerifyBitmap(FileBucket bmp)
        {
            using var bhr = new GitBitmapHeaderBucket(bmp.NoClose());

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

        protected override async ValueTask<(GitIdType IdType, int ChunkCount, long ChunkTableOffset)> ReadHeaderAsync()
        {
            if (ChunkReader is null)
                throw new InvalidOperationException();

            var headerBuffer = new byte[12];

            if (await ChunkReader.ReadAtAsync(0, headerBuffer).ConfigureAwait(false) != headerBuffer.Length)
                return (GitIdType.None, 0, -1);

            if (!"MIDX\x01".Select(x => (byte)x).SequenceEqual(headerBuffer.Take(5)))
                return (GitIdType.None, 0, -1);

            var idType = (GitIdType)headerBuffer[5];
            int chunkCount = headerBuffer[6];
            // 7 - Number of base multi pack indexes (=0)

            int packCount = NetBitConverter.ToInt32(headerBuffer, 8);

            return (idType, chunkCount, headerBuffer.Length);
        }

        internal bool CanLoad()
        {
            InitAsync().AsTask().Wait();

            return (ChunkReader != null);
        }

        internal bool ContainsPack(string path)
        {
            if (ChunkReader == null)
                return false;

            if (_packNames is null && GetChunkLength("PNAM") is long len)
            {
                byte[] names = new byte[(int)len];
                if (ReadFromChunkAsync("PNAM", 0, names).AsTask().Result != names.Length)
                    return false;

                var packNames = new List<string>();

                int s = 0;
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i] == 0)
                    {
                        if (s + 1 < i)
                        {
                            packNames.Add(Path.GetFileNameWithoutExtension(Encoding.UTF8.GetString(names, s, i - s)));

                        }
                        s = i + 1;
                    }
                }

                _packNames = packNames.ToArray();

                _packs = packNames.Select(x => new PackObjectRepository(Repository, Path.Combine(_dir, x + ".pack"), IdType)).ToArray();
            }

            if (_packNames is null)
                return false;

            string name = Path.GetFileNameWithoutExtension(path);
            foreach (var p in _packNames)
            {
                if (string.Equals(p, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
