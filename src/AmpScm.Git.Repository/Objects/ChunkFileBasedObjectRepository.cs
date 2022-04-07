using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git.Objects
{
    internal abstract class ChunkFileBasedObjectRepository : GitObjectRepository
    {
        readonly string _fileName;
        protected GitIdType IdType { get; private set; }
        protected FileStream? ChunkStream { get; private set; }

        public ChunkFileBasedObjectRepository(GitRepository repository, string mainFile, string key) : base(repository, key)
        {
            _fileName = mainFile ?? throw new ArgumentNullException(nameof(mainFile));
        }

        protected GitId GetGitIdByIndex(uint i)
        {
            int hashLength = GitId.HashLength(IdType);
            byte[] oidData = new byte[hashLength];

            if (ReadFromChunk("OIDL", i * hashLength, oidData) != hashLength)
                throw new InvalidOperationException();

            return new GitId(IdType, oidData);
        }

        [DebuggerDisplay("{Name}, Length={Length}")]
        struct Chunk
        {
            public string? Name;
            public long Position;
            public long Length;
        }

        Chunk[]? _chunks;
        protected uint[]? FanOut { get; private set; }

        protected long AfterChunkPosition => _chunks?.Select(x => x.Position + x.Length).Last() ?? -1;

        protected async ValueTask Init()
        {
            if (FanOut is not null)
                return;

            await Task.Yield();
            ChunkStream ??= File.OpenRead(_fileName);
            ChunkStream.Seek(0, SeekOrigin.Begin);

            var (idType, chunkCount) = await ReadHeaderAsync().ConfigureAwait(false);

            if (chunkCount == 0)
            {
#if !NETFRAMEWORK
                await ChunkStream.DisposeAsync().ConfigureAwait(false);
#else
                ChunkStream.Dispose();
#endif
                ChunkStream = null;
                return;
            }
            IdType = idType;


            await ReadChunks(chunkCount).ConfigureAwait(false);
        }

        protected abstract ValueTask<(GitIdType IdType, int ChunkCount)> ReadHeaderAsync();

        async ValueTask ReadChunks(int chunkCount)
        {
            if (ChunkStream is null)
                throw new InvalidOperationException();

            var chunkTable = new byte[(chunkCount + 1) * (4 + sizeof(long))];

#if !NETFRAMEWORK
            if (await ChunkStream.ReadAsync(chunkTable, CancellationToken.None).ConfigureAwait(false) != chunkTable.Length)
                return;
#else
            if (await ChunkStream.ReadAsync(chunkTable, 0, chunkTable.Length, CancellationToken.None).ConfigureAwait(false) != chunkTable.Length)
                return;
#endif

            _chunks = Enumerable.Range(0, chunkCount + 1).Select(i => new Chunk
            {
                Name = (i < chunkCount) ? Encoding.ASCII.GetString(chunkTable, 12 * i, 4) : null,
                Position = NetBitConverter.ToInt64(chunkTable, 12 * i + 4)
            }).ToArray();

            for (int i = 0; i < chunkCount; i++)
            {
                _chunks[i].Length = _chunks[i + 1].Position - _chunks[i].Position;
            }

            FanOut = ReadFanOut();
        }

        protected virtual uint[]? ReadFanOut()
        {
            byte[] fanOut = new byte[256 * sizeof(int)];

            if (ReadFromChunk("OIDF", 0, fanOut) != fanOut.Length)
                return null;

            return Enumerable.Range(0, 256).Select(i => NetBitConverter.ToUInt32(fanOut, sizeof(int) * i)).ToArray();
        }


        protected int ReadFromChunk(string chunkType, long position, byte[] buffer)
        {
            return ReadFromChunk(chunkType, position, buffer, 0, buffer.Length);
        }

        protected int ReadFromChunk(string chunkType, long position, byte[] buffer, int offset, int length)
        {
            if (_chunks == null || ChunkStream == null)
                return 0;

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
                return 0;

            ChunkStream.Seek(ch.Value.Position + position, SeekOrigin.Begin);

            int requested = (int)Math.Min(length, ch.Value.Length - position);

            return ChunkStream.Read(buffer, offset, requested);
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

        protected bool TryFindId(GitId id, out uint index)
        {
            if (FanOut == null)
            {
                index = 0;
                return false;
            }

            uint first = (id[0] == 0) ? 0 : FanOut[id[0] - 1];
            uint count = FanOut[id[0]];

            uint c = count;

            while (first < c - 1)
            {
                uint mid = (first + c) / 2;

                var check = GetGitIdByIndex(mid);

                int n = id.HashCompare(check);

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
                index = count;
                return false;
            }

            var check2 = GetGitIdByIndex(first);
            index = first;

            int n2 = id.HashCompare(check2);

            if (n2 == 0)
                return true;
            else if (n2 > 0)
                index++;

            return false;
        }
    }
}
