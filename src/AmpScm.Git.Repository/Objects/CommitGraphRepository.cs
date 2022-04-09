using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git.Objects
{
    internal sealed class CommitGraphRepository : ChunkFileBasedObjectRepository
    {
        public CommitGraphRepository(GitRepository repository, string chainFile)
            : base(repository, chainFile, "CommitGraph:" + chainFile)
        {
        }

        public override async IAsyncEnumerable<TGitObject> GetAll<TGitObject>(HashSet<GitId> alreadyReturned)
        {
            if (!typeof(TGitObject).IsAssignableFrom(typeof(GitCommit)))
                yield break;

            await InitAsync().ConfigureAwait(false);

            for (uint i = 0; i < (FanOut?[255] ?? 0); i++)
            {
                var oid = await GetOidAsync(i).ConfigureAwait(false);

                if (!alreadyReturned.Contains(oid))
                    yield return (TGitObject)(object)new GitCommit(Repository, new LazyGitObjectBucket(Repository, oid, GitObjectType.Commit), oid);
            }
        }

        protected override async ValueTask<(GitIdType IdType, int ChunkCount, long ChunkTableOffset)> ReadHeaderAsync()
        {
            if (ChunkReader is null)
                throw new InvalidOperationException();

            var headerBuffer = new byte[8];
            if (await ChunkReader.ReadAtAsync(0, headerBuffer).ConfigureAwait(false) != headerBuffer.Length)
                return (GitIdType.None, 0, -1);

            if (!"CGPH\x01".Select(x => (byte)x).SequenceEqual(headerBuffer.Take(5)))
                return (GitIdType.None, 0, -1);

            var idType = (GitIdType)headerBuffer[5];
            int chunkCount = headerBuffer[6];
            int baseCommitGraphs = headerBuffer[7];

            return (idType, chunkCount, headerBuffer.Length);
        }

        private async ValueTask<GitId> GetOidAsync(uint i)
        {
            int hashLength = GitId.HashLength(IdType);
            byte[] oidData = new byte[hashLength];

            if (hashLength != await ReadFromChunkAsync("OIDL", i * hashLength, oidData).ConfigureAwait(false))
                throw new InvalidOperationException();

            return new GitId(IdType, oidData);
        }


        internal override async ValueTask<IGitCommitGraphInfo?> GetCommitInfo(GitId id)
        {
            await InitAsync().ConfigureAwait(false);

            var (success, index) = await TryFindIdAsync(id).ConfigureAwait(false);

            if (success)
            {
                int hashLength = GitId.HashLength(IdType);
                int commitDataSz = hashLength + 2 * sizeof(uint) + sizeof(ulong);
                byte[] commitData = new byte[commitDataSz];

                if (commitDataSz != await ReadFromChunkAsync("CDAT", index * commitDataSz, commitData).ConfigureAwait(false))
                    return null;

                // commitData now contains the root hash, 2 parent indexes and the topological level
                uint parent0 = NetBitConverter.ToUInt32(commitData, hashLength);
                uint parent1 = NetBitConverter.ToUInt32(commitData, hashLength + sizeof(uint));
                ulong chainLevel = NetBitConverter.ToUInt64(commitData, hashLength + 2 * sizeof(uint));

                Task<GitId>[] parents;

                if (parent0 == 0x70000000)
                    return new GitCommitGraphInfo(Array.Empty<GitId>(), chainLevel);
                else if (parent1 == 0x70000000)
                    parents = new[] { GetOidAsync(parent0).AsTask() };
                else if (parent1 >= 0x80000000)
                {
                    var extraParents = new byte[sizeof(uint) * 256];
                    int len = await ReadFromChunkAsync("EDGE", 4 * (parent1 & 0x7FFFFFFF), extraParents).ConfigureAwait(false) / sizeof(uint);

                    if (len == 0 || len >= 256)
                        return null; // Handle as if not exists in chain. Should never happen

                    int? stopAfter = null;

                    parents = new[] { GetOidAsync(parent0).AsTask() }.Concat(
                        Enumerable.Range(0, len)
                            .Select(i => NetBitConverter.ToUInt32(extraParents, i * sizeof(uint)))
                            .TakeWhile((v, i) => { if (i > stopAfter) return false; else if ((v & 0x80000000) != 0) { stopAfter = i; }; return true; })
                            .Select(v => GetOidAsync(v & 0x7FFFFFFF).AsTask())).ToArray();
                }
                else
                    parents = new[] { GetOidAsync(parent0).AsTask(), GetOidAsync(parent1).AsTask() };

                await Task.WhenAll(parents).ConfigureAwait(false);

                return new GitCommitGraphInfo(parents.Select(x=>x.Result).ToArray(), chainLevel);
            }

            return null;
        }

        internal override bool ProvidesGetObject => false;
    }
}
