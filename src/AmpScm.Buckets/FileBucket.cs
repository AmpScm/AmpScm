using System.ComponentModel;
using System.Diagnostics;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets;

[DebuggerDisplay($"{{{nameof(SafeName)},nq}}: Position={{{nameof(Position)}}}, Next={{{nameof(PeekDisplay)},nq}}")]
public sealed partial class FileBucket : Bucket, IBucketPoll, IBucketSeek, IBucketSkip, IBucketSeekOnReset, IBucketNoDispose, IBucketDuplicateSeekedAsync
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private FileHolder _holder;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly byte[] _buffer;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _size;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _pos;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private long _filePos;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private long _bufStart;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private long _resetPosition;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _nDispose;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly int _chunkSizeMinus1;

    private FileBucket(FileHolder holder, int bufferSize = 8192, int chunkSize = 4096)
    {
        if (bufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize), bufferSize, message: null);
        if (chunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, message: null);

        _holder = holder ?? throw new ArgumentNullException(nameof(holder));
        _holder.AddRef();
        _buffer = new byte[bufferSize];
        _chunkSizeMinus1 = chunkSize - 1;
        _bufStart = -bufferSize;
        _nDispose = 1;
    }

    /// <summary>
    /// Creates a filebucket with specific buffer and chunksize. Values should be tweaked
    /// for the specific usecase (random or sequential IO)
    /// </summary>
    /// <param name="path"></param>
    /// <param name="bufferSize">The total buffersize. Typically chunkSize*N</param>
    /// <param name="chunkSize">The chunksize to perform file IO with.</param>
    [EditorBrowsable(EditorBrowsableState.Advanced)] // Users should use FileBucket.Open(...)
    public FileBucket(string path, int bufferSize = 8192, int chunkSize = 4096)
        : this(OpenHolder(path, forAsync: true), bufferSize, chunkSize)
    {
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        try
        {
            if (disposing)
            {
                int n = Interlocked.Decrement(ref _nDispose);

                if (n == 0)
                {
                    try
                    {
                        InnerDispose();
                    }
                    catch (ObjectDisposedException oe)
                    {
                        throw new ObjectDisposedException($"While disposing {SafeName}", oe);

                    }
                }
#if DEBUG && NET7_0_OR_GREATER
                else
                    ObjectDisposedException.ThrowIf(n < 0, this);
#elif DEBUG
                else if (n < 0)
                    throw new ObjectDisposedException(SafeName);
#endif
            }
        }
        finally
        {
            await base.DisposeAsync(disposing);
        }
    }

    private void InnerDispose()
    {
        try
        {
            _holder?.Release();
        }
        finally
        {
            _holder = null!;

        }
    }

    public override ValueTask<long?> ReadRemainingBytesAsync()
    {
        return new ValueTask<long?>(_holder.Length - _filePos);
    }

    public override string Name => "File[" + _holder?.Path + "]";

    public override long? Position => _filePos;

    /// <summary>
    /// Gets the total file length
    /// </summary>
    public long Length => _holder.Length;

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
            throw new ArgumentOutOfRangeException(nameof(minRequested), minRequested, message: null);

        if (_pos < _size)
            return new BucketBytes(_buffer, _pos, _size - _pos);

        await RefillAsync(minRequested).ConfigureAwait(false);

        if (_pos < _size)
            return new BucketBytes(_buffer, _pos, _size - _pos);
        else
            return BucketBytes.Eof;
    }

    public override bool CanReset => true;

    public override void Reset()
    {
        _pos = _size;
        _filePos = _resetPosition;
    }

    public ValueTask SeekAsync(long newPosition)
    {
        if (newPosition < 0)
            throw new ArgumentOutOfRangeException(nameof(newPosition), newPosition, message: null);

        if (newPosition > _bufStart && newPosition < _bufStart + _size)
        {
            _pos = (int)(newPosition - _bufStart);
            _filePos = newPosition;
        }
        else if (newPosition > _holder.Length)
            throw new BucketException($"Seek after end of {Name} requested");
        else
        {
            _pos = _size; // Empty buffer
            _filePos = newPosition;
        }

        return default;
    }

    async ValueTask<Bucket> IBucketDuplicateSeekedAsync.DuplicateSeekedAsync(long newPosition)
    {
        return await DuplicateSeekedAsync(newPosition).ConfigureAwait(false);
    }

    public async ValueTask<FileBucket> DuplicateSeekedAsync(long newPosition)
    {
        var b = (FileBucket)Duplicate(Math.Abs(_pos - newPosition) > 8192);

        await b.SeekAsync(newPosition).ConfigureAwait(false);

        return b;
    }

    Bucket IBucketSeekOnReset.SeekOnReset()
    {
        if (_nDispose > 1)
        {
            var b = (FileBucket)Duplicate();
            b._resetPosition = Position!.Value;
            this.DisposeAsync().AsTask().GetAwaiter().GetResult();

            return b;
        }
        else
        {
            _resetPosition = Position!.Value;
            return this;
        }
    }

    Bucket IBucketNoDispose.NoDispose()
    {
        Interlocked.Increment(ref _nDispose);
        return this;
    }

    bool IBucketNoDispose.HasMultipleDisposers()
    {
        return _nDispose > 1;
    }

    Bucket IBucketSkip.Skip(long skipBytes, bool ensure)
    {
        if (_size - _pos > skipBytes)
        {
            int skip = (int)skipBytes;

            _pos += skip;
            _filePos += skip;
        }
        else
        {
            _pos = _size;

            _filePos = Math.Min(_filePos + skipBytes, _holder.Length);
        }

        return this;
    }

    public override Bucket Duplicate(bool reset = false)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        FileBucket fbNew = new FileBucket(_holder, _buffer.Length, _chunkSizeMinus1 + 1);
#pragma warning restore CA2000 // Dispose objects before losing scope

        fbNew._resetPosition = _resetPosition;
        if (reset)
            fbNew._filePos = _resetPosition;
        else
            fbNew._filePos = _filePos;

        if (!reset && fbNew._buffer.Length == _buffer.Length)
        {
            _buffer.AsSpan().CopyTo(fbNew._buffer);
            fbNew._bufStart = _bufStart;
            fbNew._size = _size;
            fbNew._pos = _pos;
        }

        return fbNew;
    }

    private const int MinCache = 16; // Only use the existing cache instead of seek when at least this many bytes are available

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        if (requested <= 0)
            throw new ArgumentOutOfRangeException(nameof(requested), requested, message: null);

        if (_pos < _size)
        {
            BucketBytes data = new BucketBytes(_buffer, _pos, Math.Min(requested, _size - _pos));
            _pos += data.Length;
            _filePos += data.Length;
            return data;
        }

        await RefillAsync(requested).ConfigureAwait(false);

        if (_pos == _size)
            return BucketBytes.Eof;

        BucketBytes result = new BucketBytes(_buffer, _pos, Math.Min(requested, _size - _pos));
        _pos += result.Length;
        _filePos += result.Length;

        Debug.Assert(result.Length > 0);

        return result;
    }

    private async ValueTask RefillAsync(int requested)
    {
        Debug.Assert(requested <= MaxRead);

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
            else if (_bufStart + _size == Length && basePos == _bufStart)
            {
                // We are at the end of the file, and have that data. No need to peek further
            }
            else
            {
                _size = await ReadAtAsync(basePos, _buffer, readLen).ConfigureAwait(false);
                _bufStart = basePos;
            }
        }
        _pos = (int)(_filePos - _bufStart);

        if (_size == 0 || _pos == _size)
        {
            _pos = _size;
        }
    }

    /// <summary>
    /// Read <paramref name="requested"/> bytes from the file starting at offset <paramref name="fileOffset"/>
    /// into <paramref name="buffer"/>. Unlike the normal bucket functions, this function's task will only complete
    /// when as many bytes as available in the file are read into buffer.
    /// </summary>
    /// <param name="fileOffset"></param>
    /// <param name="buffer"></param>
    /// <param name="requested"></param>
    /// <returns>The number of bytes actually read</returns>
    /// <remarks><para>This function exposes the underlying file and doesn't affect this file as <see cref="Bucket"/> and
    /// as such doesn't change any of the buffering rules as documented on <see cref="Bucket"/></para>
    ///
    /// <para>Normal users of <see cref="FileBucket"/> shouldn't need this feature, but it might help implementations
    /// that use the same file for streaming <see cref="Bucket"/> IO and as explicit indexes.</para></remarks>
    public ValueTask<int> ReadAtAsync(long fileOffset, byte[] buffer, int requested)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));

        return _holder.ReadAtAsync(fileOffset, buffer, requested);
    }

    /// <inheritdoc cref="ReadAtAsync(long, byte[], int)"/>
    public ValueTask<int> ReadAtAsync(long fileOffset, byte[] buffer)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));

        return ReadAtAsync(fileOffset, buffer, buffer.Length);
    }

    public override ValueTask<long> ReadSkipAsync(long requested)
    {
        if (requested <= 0)
            throw new ArgumentOutOfRangeException(nameof(requested), requested, message: null);

        if (_size - _pos > requested)
            return base.ReadSkipAsync(requested);
        else
        {
            _pos = _size;

            long newPos = Math.Min(_filePos + requested, _holder.Length);

            int skipped = (int)(newPos - _filePos);
            _filePos = newPos;
            return new(skipped);
        }
    }

    /// <summary>
    /// Opens the file at <paramref name="path"/> as a <see cref="FileBucket"/>. If <paramref name="forAsync"/> is true,
    /// requests async support from the operating system for multiple operations at the same time, otherwise
    /// all requests will be handled synchronously.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="forAsync"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <remarks>This class has an optimized code path for Windows so async and non-async IO have
    /// ballpark similar performance even on older .Net version by checking for synchronous success,
    /// unlike the standard <see cref="FileStream"/>, which goes fully async when you enable this
    /// flag.</remarks>
    public static FileBucket OpenRead(string path, bool forAsync)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        return new FileBucket(OpenHolder(path, forAsync));
    }

    private static FileHolder OpenHolder(string path, bool forAsync)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        FileStream? primary = null;
        FileHolder? fh = null;

        if (forAsync)
        {
#if !NETFRAMEWORK
            if (OperatingSystem.IsWindows())
#else
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
#endif
                fh = new FileHolder(FileHolder.OpenAsyncWin32(path), path);
            else
                primary = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
        }
        else
            primary = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096);

        return fh ??= new FileHolder(primary!, path);
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

    /// <summary>
    /// Opens file <paramref name="path"/> for asynchronous IO as <see cref="FileBucket"/>
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static FileBucket OpenRead(string path)
    {
        return OpenRead(path, forAsync: true);
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

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string PeekDisplay => ((_size - _pos > 0) ? _buffer.AsMemory(_pos, _size - _pos) : new Memory<byte>()).AsDebuggerDisplay();
}
