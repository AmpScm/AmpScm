using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git.Objects;

internal sealed class GitCommitGraph : ChunkFileBasedObjectRepository
{
    private bool _haveV2;
    private int _baseCommitGraphs;
    private readonly CommitGraphChain? _graphRoot;
    internal readonly GitId? GraphId;
    private int? _oidOffset;

    public GitCommitGraph(GitRepository repository, string chainFile)
        : base(repository, chainFile, "CommitGraph:" + chainFile)
    {
    }

    public GitCommitGraph(GitRepository repository, string chainFile, CommitGraphChain graphRoot, GitId graphId)
        : this(repository, chainFile)
    {
        _graphRoot = graphRoot;
        GraphId = graphId;
    }

    public override async IAsyncEnumerable<TGitObject> GetAll<TGitObject>(HashSet<GitId> alreadyReturned)
    {
        if (!typeof(TGitObject).IsAssignableFrom(typeof(GitCommit)))
            yield break;

        await InitAsync().ConfigureAwait(false);

        int baseOffset = this.OidOffset;
        for (uint i = 0; i < OidCount; i++)
        {
            var oid = await GetOidAsync((int)i + baseOffset).ConfigureAwait(false);

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

        if (baseCommitGraphs <= 0)
            _baseCommitGraphs = -1;
        else
            _baseCommitGraphs = baseCommitGraphs;

        return (idType, chunkCount, headerBuffer.Length);
    }

    protected override async ValueTask InitAsync()
    {
        await base.InitAsync().ConfigureAwait(false);

        _haveV2 = GetChunkLength("GDA2").HasValue;

        if (_baseCommitGraphs > 0)
        {
            byte[] baseGraphs = new byte[_baseCommitGraphs * IdType.HashLength()];

            if (baseGraphs.Length != await ReadFromChunkAsync("BASE", 0, baseGraphs).ConfigureAwait(false))
            {
                throw new GitException($"Can't read commit-graph base chunks from {ChunkReader} Bucket");
            }

            GitId parentId = GitId.FromByteArrayOffset(IdType, baseGraphs, (_baseCommitGraphs - 1) * IdType.HashLength());

            ParentGraph = _graphRoot!.GetCommitGraph(parentId);
        }
    }

    public int OidCount => (int)(FanOut?[255] ?? 0);

    public int OidOffset
    {
        get
        {
            if (!_oidOffset.HasValue)
            {
                if (_baseCommitGraphs < 0)
                    _oidOffset = 0;
                else if (_baseCommitGraphs > 0)
                {
                    _oidOffset = ParentGraph!.OidOffset + ParentGraph.OidCount;
                }
            }

            return _oidOffset!.Value;
        }
    }

    private async ValueTask<GitId> GetOidAsync(int index)
    {
        int offset = OidOffset;

        if (index < offset)
            return await ParentGraph!.GetOidAsync(index).ConfigureAwait(false);

        index -= offset;

        int hashLength = GitId.HashLength(IdType);
        byte[] oidData = new byte[hashLength];

        if (hashLength != await ReadFromChunkAsync("OIDL", index * hashLength, oidData).ConfigureAwait(false))
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
                return new GitCommitGraphInfo(Array.Empty<GitId>(), chainLevel, _haveV2 ? await ReadCommitTimeOffset(index).ConfigureAwait(false) : long.MinValue);
            else if (parent1 == 0x70000000)
                parents = new[] { GetOidAsync((int)parent0).AsTask() };
            else if (parent1 >= 0x80000000)
            {
                var extraParents = new byte[sizeof(uint) * 256];
                int len = await ReadFromChunkAsync("EDGE", 4 * (parent1 & 0x7FFFFFFF), extraParents).ConfigureAwait(false) / sizeof(uint);

                if (len == 0 || len >= 256)
                    return null; // Handle as if not exists in chain. Should never happen

                int? stopAfter = null;

                parents = new[] { GetOidAsync((int)parent0).AsTask() }.Concat(
                    Enumerable.Range(0, len)
                        .Select(i => NetBitConverter.ToUInt32(extraParents, i * sizeof(uint)))
                        .TakeWhile((v, i) => { if (i > stopAfter) return false; else if ((v & 0x80000000) != 0) { stopAfter = i; } return true; })
                        .Select(v => GetOidAsync((int)(v & 0x7FFFFFFF)).AsTask())).ToArray();
            }
            else
                parents = new[] { GetOidAsync((int)parent0).AsTask(), GetOidAsync((int)parent1).AsTask() };

            IEnumerable<Task> waits;
            Task<long>? v2 = null;
            if (_haveV2)
            {
                v2 = ReadCommitTimeOffset(index).AsTask();
                waits = parents.Concat(new Task[] { v2 });
            }
            else
                waits = parents;

            await Task.WhenAll(waits).ConfigureAwait(false);

            long offset = v2 != null ? await v2.ConfigureAwait(false) : long.MinValue;

            return new GitCommitGraphInfo(parents.Select(x => x.Result).ToArray(), chainLevel, offset);
        }

        return null;
    }

    private async ValueTask<long> ReadCommitTimeOffset(uint index)
    {
        var offsetData = new byte[sizeof(int)];

        if (offsetData.Length != await ReadFromChunkAsync("GDA2", index * sizeof(int), offsetData).ConfigureAwait(false))
            return long.MinValue;

        int v = NetBitConverter.ToInt32(offsetData, 0);

        if (v >= 0)
            return v;

        offsetData = new byte[sizeof(long)];
        if (offsetData.Length != await ReadFromChunkAsync("GDO2", (~v) * sizeof(long), offsetData).ConfigureAwait(false))
            return long.MinValue;

        return NetBitConverter.ToInt64(offsetData, 0);
    }

    internal override bool ProvidesGetObject => false;

    public GitCommitGraph? ParentGraph { get; private set; }
}
