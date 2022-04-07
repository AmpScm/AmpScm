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

namespace AmpScm.Git.Objects
{
    internal class MultiPackObjectRepository : ChunkFileBasedObjectRepository
    {
        readonly string _dir;
        private string[]? _packNames;
        PackObjectRepository[]? _packs;
        GitId? _multiPackId;
        Lazy<bool> HasBitmap;
        FileBucket? _bitmapBucket;
        FileBucket? _revIdxBucket;

        public MultiPackObjectRepository(GitRepository repository, string multipackFile) : base(repository, multipackFile, "MultiPack:" + repository.GitDir)
        {
            _dir = Path.GetDirectoryName(multipackFile)!;
            HasBitmap = new Lazy<bool>(GetHasBitmap);
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
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        bool GetHasBitmap()
        {
            if (_multiPackId == null)
            {
                if (ChunkStream is null)
                    return false;

                ChunkStream!.Seek(AfterChunkPosition, SeekOrigin.Begin);
                byte[] multiPackId = new byte[Repository.InternalConfig.IdType.HashLength()];
                if (multiPackId.Length != ChunkStream.Read(multiPackId, 0, multiPackId.Length))
                    return false;

                _multiPackId = new GitId(Repository.InternalConfig.IdType, multiPackId);
            }

            return File.Exists(Path.Combine(_dir, $"multi-pack-index-{_multiPackId}.bitmap"));
        }

        public override async ValueTask<TGitObject?> GetByIdAsync<TGitObject>(GitId id)
            where TGitObject : class
        {
            if (TryFindId(id, out var index))
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
            if (ReadFromChunk("OOFF", index * result.Length, result) == result.Length)
            {
                int pack = NetBitConverter.ToInt32(result, 0);
                long offset = NetBitConverter.ToUInt32(result, 4);

                if (offset > int.MaxValue && GetChunkLength("LOFF") != null) // If not we have 32 bits
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
            where TGitObject: class
        {
            if (_packs == null)
                return (null, true); // Not really loaded yet

            if (FanOut == null)
                await Init().ConfigureAwait(false);

            uint count = FanOut![255];

            if (TryFindId(baseGitId, out var index) || (index >= 0 && index < count))
            {
                GitId foundId = GetGitIdByIndex(index);

                if (!foundId.ToString().StartsWith(idString, StringComparison.OrdinalIgnoreCase))
                    return (null, true); // Not a match, but success


                if (index + 1 < count)
                {
                    GitId next = GetGitIdByIndex(index + 1);

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
                Init().AsTask().GetAwaiter().GetResult();

            return TryFindId(id, out var _);
        }

        public override async IAsyncEnumerable<TGitObject> GetAll<TGitObject>(HashSet<GitId> alreadyReturned)
        {
            if (_packs == null)
                yield break; // Not really loaded yet

#if NOT_YET
            if (typeof(TGitObject) != typeof(GitObject) && HasBitmap.Value)
            {
                await foreach (var x in GetAllViaBitmap<TGitObject>(alreadyReturned))
                {
                    yield return x;
                }
            }
            else
#endif
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
            where TGitObject : class
        {
            if (FanOut is null || FanOut[255] == 0)
            {
                await Init().ConfigureAwait(false);
                if (FanOut is null || FanOut[255] == 0)
                    yield break;
            }

            if (_bitmapBucket == null)
            {
                var bmp = FileBucket.OpenRead(Path.Combine(_dir, $"multi-pack-index-{_multiPackId}.bitmap"));

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
                                yield return await GetOneViaMultiPackOffset<TGitObject>(bit + n, gitObjectType).ConfigureAwait(false);
                            }
                        }
                    }
                }
                bit += 8;
            }
        }

        private async ValueTask<TGitObject> GetOneViaMultiPackOffset<TGitObject>(int v, GitObjectType gitObjectType)
            where TGitObject : class
        {
            if (_revIdxBucket is null && !File.Exists(Path.Combine(_dir, $"multi-pack-index-{_multiPackId}.rev")))
            {
                //await CreateReverseIndex().ConfigureAwait(false);
                throw new NotImplementedException();
            }

            _revIdxBucket ??= FileBucket.OpenRead(Path.Combine(_dir, $"multi-pack-index-{_multiPackId}.bitmap"));
            await _revIdxBucket.ResetAsync().ConfigureAwait(false);
            await _revIdxBucket.ReadSkipAsync(12 + sizeof(uint) * v).ConfigureAwait(false);
            var indexOffs = await _revIdxBucket.ReadNetworkUInt32Async().ConfigureAwait(false);

            //byte[] oids = GetOidArray(indexOffs, 1);
            //byte[] offsets = GetOffsetArray(indexOffs, 1, oids);
            //
            //GitId objectId = GitId.FromByteArrayOffset(_idType, oids, 0);
            //
            //var rdr = await _packBucket!.DuplicateAsync(true).ConfigureAwait(false);
            //await rdr.ReadSkipAsync(GetOffset(offsets, 0)).ConfigureAwait(false);
            //
            //GitPackFrameBucket pf = new GitPackFrameBucket(rdr, _idType, MyResolveByOid);
            //
            //return (TGitObject)(object)await GitObject.FromBucketAsync(Repository, pf, objectId, gitObjectType).ConfigureAwait(false);
            throw new NotImplementedException();
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

        protected override async ValueTask<(GitIdType IdType, int ChunkCount)> ReadHeaderAsync()
        {
            if (ChunkStream is null)
                throw new InvalidOperationException();

            ChunkStream.Seek(0, SeekOrigin.Begin);
            var headerBuffer = new byte[12];
#if !NETFRAMEWORK
            if (await ChunkStream.ReadAsync(new Memory<byte>(headerBuffer), CancellationToken.None).ConfigureAwait(false) != headerBuffer.Length)
#else
            if (await ChunkStream.ReadAsync(headerBuffer, 0, headerBuffer.Length, CancellationToken.None).ConfigureAwait(false) != headerBuffer.Length)
#endif
                return (GitIdType.None, 0);

            if (!"MIDX\x01".Select(x => (byte)x).SequenceEqual(headerBuffer.Take(5)))
                return (GitIdType.None, 0);

            var idType = (GitIdType)headerBuffer[5];
            int chunkCount = headerBuffer[6];
            // 7 - Number of base multi pack indexes (=0)

            int packCount = NetBitConverter.ToInt32(headerBuffer, 8);

            return (idType, chunkCount);
        }

        internal bool CanLoad()
        {
            Init().AsTask().GetAwaiter().GetResult();

            return (ChunkStream != null);
        }

        internal bool ContainsPack(string path)
        {
            if (ChunkStream == null)
                return false;

            if (_packNames is null && GetChunkLength("PNAM") is long len)
            {
                byte[] names = new byte[(int)len];
                if (ReadFromChunk("PNAM", 0, names) != names.Length)
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
