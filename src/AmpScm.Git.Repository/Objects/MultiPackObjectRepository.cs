using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git.Objects
{
    internal class MultiPackObjectRepository : ChunkFileBasedObjectRepository
    {
        readonly string _dir;
        private string[]? _packNames;
        PackObjectRepository[]? _packs;

        public MultiPackObjectRepository(GitRepository repository, string multipackFile) : base(repository, multipackFile, "MultiPack:" + repository.GitDir)
        {
            _dir = Path.GetDirectoryName(multipackFile)!;
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
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
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

            // Prefer locality of packs, over the multipack order when not using bitmaps
            foreach (var p in _packs)
            {
                await foreach (var x in p.GetAll<TGitObject>(alreadyReturned))
                {
                    yield return x;
                }
            }
        }

        protected override async ValueTask<(GitIdType IdType, int ChunkCount)> ReadHeaderAsync()
        {
            if (ChunkStream is null)
                throw new InvalidOperationException();

            ChunkStream.Seek(0, SeekOrigin.Begin);
            var headerBuffer = new byte[12];
            if (await ChunkStream.ReadAsync(headerBuffer, 0, headerBuffer.Length, CancellationToken.None).ConfigureAwait(false) != headerBuffer.Length)
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
