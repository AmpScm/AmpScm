﻿using System.Diagnostics;
using System.Text;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git.Objects;

internal abstract class ChunkFileBasedObjectRepository : GitObjectRepository
{
    private readonly string _fileName;
    protected GitIdType IdType { get; private set; }
    protected FileBucket? ChunkReader { get; private set; }

    private protected ChunkFileBasedObjectRepository(GitRepository repository, string mainFile, string key) : base(repository, key)
    {
        _fileName = mainFile ?? throw new ArgumentNullException(nameof(mainFile));
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                ChunkReader?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    protected async ValueTask<GitId> GetGitIdByIndexAsync(uint i)
    {
        int hashLength = GitId.HashLength(IdType);
        byte[] oidData = new byte[hashLength];

        if (await ReadFromChunkAsync("OIDL", i * hashLength, oidData).ConfigureAwait(false) != hashLength)
            throw new InvalidOperationException();

        return new GitId(IdType, oidData);
    }

    [DebuggerDisplay("{Name}, Length={Length}")]
    private struct Chunk
    {
        public string? Name;
        public long Position;
        public long Length;
    }

    private Chunk[]? _chunks;
    protected uint[]? FanOut { get; private set; }

    protected long AfterChunkPosition => _chunks?.Select(x => x.Position + x.Length).Last() ?? -1;

    protected virtual async ValueTask InitAsync()
    {
        if (FanOut is not null)
            return;

        await Task.Yield();
        ChunkReader ??= FileBucket.OpenRead(_fileName);

        var (idType, chunkCount, chunkTableOffset) = await ReadHeaderAsync().ConfigureAwait(false);

        if (chunkCount == 0)
        {
            await ChunkReader.DisposeAsync();
            ChunkReader = null;
            return;
        }
        IdType = idType;


        await ReadChunks(chunkTableOffset, chunkCount).ConfigureAwait(false);
    }

    protected abstract ValueTask<(GitIdType IdType, int ChunkCount, long ChunkTableOffset)> ReadHeaderAsync();

    private async ValueTask ReadChunks(long chunkTableOffset, int chunkCount)
    {
        if (ChunkReader is null)
            throw new InvalidOperationException();

        var chunkTable = new byte[(chunkCount + 1) * (4 + sizeof(long))];

        if (await ChunkReader.ReadAtAsync(chunkTableOffset, chunkTable).ConfigureAwait(false) != chunkTable.Length)
            return;

        _chunks = Enumerable.Range(0, chunkCount + 1).Select(i => new Chunk
        {
            Name = (i < chunkCount) ? Encoding.ASCII.GetString(chunkTable, 12 * i, 4) : null,
            Position = NetBitConverter.ToInt64(chunkTable, 12 * i + 4)
        }).ToArray();

        for (int i = 0; i < chunkCount; i++)
        {
            _chunks[i].Length = _chunks[i + 1].Position - _chunks[i].Position;
        }

        FanOut = await ReadFanOutAsync().ConfigureAwait(false);
    }

    protected virtual async ValueTask<uint[]?> ReadFanOutAsync()
    {
        byte[] fanOut = new byte[256 * sizeof(int)];

        if (await ReadFromChunkAsync("OIDF", 0, fanOut).ConfigureAwait(false) != fanOut.Length)
            return null;

        return Enumerable.Range(0, 256).Select(i => NetBitConverter.ToUInt32(fanOut, sizeof(int) * i)).ToArray();
    }

    protected ValueTask<int> ReadFromChunkAsync(string chunkType, long position, byte[] buffer)
    {
        return ReadFromChunkAsync(chunkType, position, buffer, buffer.Length);
    }

    protected ValueTask<int> ReadFromChunkAsync(string chunkType, long position, byte[] buffer, int length)
    {
        if (_chunks == null || ChunkReader == null)
            return new(0);
        else if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, message: null);

        Chunk? ch = null;
        foreach (var c in _chunks)
        {
            if (c.Name == chunkType)
            {
                ch = c;
                break;
            }
        }
        if (ch == null)
            return new(0);

        int requested = (int)Math.Min(length, ch.Value.Length - position);

        if (requested <= 0)
            return new(0);

        return ChunkReader.ReadAtAsync(ch.Value.Position + position, buffer, requested);
    }

    protected long? GetChunkLength(string chunkType)
    {
        if (_chunks != null)
            foreach (var c in _chunks)
            {
                if (c.Name == chunkType)
                    return c.Length;
            }

        return null;
    }

    protected async ValueTask<(bool Success, uint Index)> TryFindIdAsync(GitId id)
    {
        if (FanOut == null)
        {
            return (false, 0);
        }

        uint first = (id[0] == 0) ? 0 : FanOut[id[0] - 1];
        uint countAfter = FanOut[id[0]];

        if (first == countAfter) // Includes countAfter=0, which overflows the loop
        {
            // No need to check.
            return (false, 0);
        }

        uint c = countAfter;
        while (first + 1 < c)
        {
            uint mid = first + (c - first) / 2;

            var check = await GetGitIdByIndexAsync(mid).ConfigureAwait(false);

            int n = id.CompareTo(check);

            if (n == 0)
            {
                return (true, mid);
            }
            else if (n < 0)
                c = mid;
            else
                first = mid + 1;
        }

        if (first >= countAfter)
        {
            return (false, countAfter);
        }

        var check2 = await GetGitIdByIndexAsync(first).ConfigureAwait(false);

        int n2 = id.CompareTo(check2);

        if (n2 == 0)
            return (true, first);
        else if (n2 > 0)
            first++;

        return (false, first);
    }
}
