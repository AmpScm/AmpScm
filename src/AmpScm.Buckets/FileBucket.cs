using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets
{
    public sealed partial class FileBucket : Bucket, IBucketPoll
    {
        readonly FileHolder _holder;
        readonly byte[] _buffer;
        int _size;
        int _pos;
        long _filePos;
        long _bufStart;
        readonly int _chunkSizeMinus1;

        private FileBucket(FileHolder holder, int bufferSize = 8192, int chunkSize = 2048)
        {
            _holder = holder ?? throw new ArgumentNullException(nameof(holder));
            _holder.AddRef();
            _buffer = new byte[bufferSize];
            _chunkSizeMinus1 = chunkSize - 1;
            _bufStart = -bufferSize;
        }

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            return new ValueTask<long?>(_holder.Length - _filePos);
        }

        public override string Name => "File";

        public override long? Position => _filePos;

        public override BucketBytes Peek()
        {
            if (_pos < _size)
                return new BucketBytes(_buffer, _pos, _size - _pos);
            else
                return BucketBytes.Empty;
        }

        async ValueTask<BucketBytes> IBucketPoll.PollAsync(int minRequested /*= 1*/)
        {
            if (minRequested <= 0)
                throw new ArgumentOutOfRangeException(nameof(minRequested));

            if (_pos < _size)
                return new BucketBytes(_buffer, _pos, _size - _pos);

            await Refill(minRequested).ConfigureAwait(false);

            if (_pos < _size)
                return new BucketBytes(_buffer, _pos, _size - _pos);
            else
                return BucketBytes.Eof;
        }

        public override bool CanReset => true;

        public override ValueTask ResetAsync()
        {
            _pos = _size;
            _filePos = 0;

            return default;
        }

        public override ValueTask<Bucket> DuplicateAsync(bool reset)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            FileBucket fbNew = new FileBucket(_holder, _buffer.Length, _chunkSizeMinus1 + 1);
#pragma warning restore CA2000 // Dispose objects before losing scope

            if (reset)
                fbNew._filePos = 0;
            else
                fbNew._filePos = _filePos;

            return new ValueTask<Bucket>(fbNew);
        }

        const int MinCache = 16; // Only use the existing cache instead of seek when at least this many bytes are available

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (requested <= 0)
                throw new ArgumentOutOfRangeException(nameof(requested));

            if (_pos < _size)
            {
                BucketBytes data = new BucketBytes(_buffer, _pos, Math.Min(requested, _size - _pos));
                _pos += data.Length;
                _filePos += data.Length;
                return data;
            }

            await Refill(requested).ConfigureAwait(false);

            if (_pos == _size)
                return BucketBytes.Eof;

            BucketBytes result = new BucketBytes(_buffer, _pos, Math.Min(requested, _size - _pos));
            _pos += result.Length;
            _filePos += result.Length;

            System.Diagnostics.Debug.Assert(result.Length > 0);

            return result;
        }

        private async Task Refill(int requested)
        {
            long basePos = _filePos & ~_chunkSizeMinus1; // Current position round back to chunk
            int extra = (int)(_filePos - basePos); // Position in chunk

            int readLen = (requested + extra + _chunkSizeMinus1) & ~_chunkSizeMinus1;

            if (readLen > _buffer.Length)
                readLen = _buffer.Length;


            if (_bufStart != basePos || readLen > _size)
            {
                if (_filePos < _bufStart + _size - Math.Min(requested, MinCache) && _filePos >= _bufStart)
                {
                    // We still have the requested data
                }
                else
                {
                    _size = await _holder.ReadAtAsync(basePos, _buffer, readLen).ConfigureAwait(false);
                    _bufStart = basePos;
                }
            }
            _pos = (int)(_filePos - _bufStart);

            if (_size == 0 || _pos == _size)
            {
                _pos = _size;
            }
        }

        public override ValueTask<int> ReadSkipAsync(int requested)
        {
            if (requested <= 0)
                throw new ArgumentOutOfRangeException(nameof(requested));

            if (_size - _pos > requested)
                return base.ReadSkipAsync(requested);
            else
            {
                _pos = _size;

                long newPos = Math.Min(_filePos + requested, _holder.Length);

                int skipped = (int)(newPos - _filePos);
                _filePos = newPos;
                return new ValueTask<int>(skipped);
            }
        }

        public static FileBucket OpenRead(string path, bool forAsync)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            FileStream? primary = null;
            FileHolder? fh = null;

            if (forAsync)
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    fh = new FileHolder(FileHolder.OpenAsyncWin32(path), path);
                else
                    primary = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
            }
            else
                primary = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096);

            fh ??= new FileHolder(primary!, path);

            return new FileBucket(fh);
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        public static FileBucket OpenRead(string path)
        {
            return OpenRead(path, true);
        }

        //public static FileBucket OpenRead(FileStream from)
        //{
        //    if (from == null)
        //        throw new ArgumentNullException(nameof(from));
        //    else if (!from.CanRead)
        //        throw new ArgumentException("Unreadable stream", nameof(from));
        //
        //    FileHolder fh = new FileHolder(from, null);
        //
        //    return new FileBucket(fh);
        //}
    }
}
