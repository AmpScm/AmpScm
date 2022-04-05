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

            await Init().ConfigureAwait(false);

            for (uint i = 0; i < (FanOut?[255] ?? 0); i++)
            {
                var oid = GetOid(i);

                if (!alreadyReturned.Contains(oid))
                    yield return (TGitObject)(object)new GitCommit(Repository, new LazyGitObjectBucket(Repository, oid, GitObjectType.Commit), oid);
            }
        }

        protected override async ValueTask<(GitIdType IdType, int ChunkCount)> ReadHeaderAsync()
        {
            if (ChunkStream is null)
                throw new InvalidOperationException();

            ChunkStream.Seek(0, SeekOrigin.Begin);
            var headerBuffer = new byte[8];
#if !NETFRAMEWORK
            if (await ChunkStream.ReadAsync(new Memory<byte>(headerBuffer), CancellationToken.None).ConfigureAwait(false) != headerBuffer.Length)
#else
            if (await ChunkStream.ReadAsync(headerBuffer, 0, headerBuffer.Length, CancellationToken.None).ConfigureAwait(false) != headerBuffer.Length)
#endif
                return (GitIdType.None, 0);

            if (!"CGPH\x01".Select(x => (byte)x).SequenceEqual(headerBuffer.Take(5)))
                return (GitIdType.None, 0);

            var idType = (GitIdType)headerBuffer[5];
            int chunkCount = headerBuffer[6];
            int baseCommitGraphs = headerBuffer[7];

            return (idType, chunkCount);
        }

        private GitId GetOid(uint i)
        {
            int hashLength = GitId.HashLength(IdType);
            byte[] oidData = new byte[hashLength];

            if (ReadFromChunk("OIDL", i * hashLength, oidData) != hashLength)
                throw new InvalidOperationException();

            return new GitId(IdType, oidData);
        }


        internal override async ValueTask<IGitCommitGraphInfo?> GetCommitInfo(GitId id)
        {
            await Init().ConfigureAwait(false);

            if (TryFindId(id, out var index))
            {
                int hashLength = GitId.HashLength(IdType);
                int commitDataSz = hashLength + 2 * sizeof(uint) + sizeof(ulong);
                byte[] commitData = new byte[commitDataSz];

                if (ReadFromChunk("CDAT", index * commitDataSz, commitData) != commitDataSz)
                    return null;

                // commitData now contains the root hash, 2 parent indexes and the topological level
                uint parent0 = NetBitConverter.ToUInt32(commitData, hashLength);
                uint parent1 = NetBitConverter.ToUInt32(commitData, hashLength + sizeof(uint));
                ulong chainLevel = NetBitConverter.ToUInt64(commitData, hashLength + 2 * sizeof(uint));

                GitId[] parents;

                if (parent0 == 0x70000000)
                    parents = Array.Empty<GitId>();
                else if (parent1 == 0x70000000)
                    parents = new[] { GetOid(parent0) };
                else if (parent1 >= 0x80000000)
                {
                    var extraParents = new byte[sizeof(uint) * 256];
                    int len = ReadFromChunk("EDGE", 4 * (parent1 & 0x7FFFFFFF), extraParents) / 4;

                    if (len == 0 || len >= 256)
                        return null; // Handle as if not exists in chain. Should never happen

                    int? stopAfter = null;
                    parents = new[] { parent0 }.Concat(
                        Enumerable.Range(0, len)
                            .Select(i => NetBitConverter.ToUInt32(extraParents, i * sizeof(uint)))
                            .TakeWhile((v, i) => { if (i > stopAfter) return false; else if ((v & 0x80000000) != 0) { stopAfter = i; }; return true; }))
                            .Select(v => GetOid(v & 0x7FFFFFFF)).ToArray();
                }
                else
                    parents = new[] { GetOid(parent0), GetOid(parent1) };

                return new GitCommitGraphInfo(parents, chainLevel);
            }

            return null;
        }

        internal override bool ProvidesGetObject => false;
    }
}
