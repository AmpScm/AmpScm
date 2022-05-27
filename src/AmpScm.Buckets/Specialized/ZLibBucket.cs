using System;
using System.Diagnostics;
using System.IO.Compression;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using Elskom.Generic.Libs;

namespace AmpScm.Buckets.Specialized
{
    [DebuggerDisplay($"{{{nameof(SafeName)},nq}}: Position={{{nameof(Position)}}}, Peek={{{nameof(DebuggerDisplay)},nq}}")]
    public sealed class ZLibBucket : WrappingBucket, IBucketPoll, IBucketSeek
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly ZStream _z;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool _eof;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool _readEof;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        BucketBytes read_buffer;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        BucketBytes write_buffer;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        byte[] write_data;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        long _position; // Zlib read position
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly BucketCompressionAlgorithm _algorithm;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly BucketCompressionLevel? _level;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly IBucketPoll? _innerPoll;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        int? _headerLeft;


        /// <summary>
        /// Default buffer size. (Value may change in the future, but current value is always guaranteed to be supported)
        /// </summary>
        public const int DefaultBufferSize = 8192;

        internal ZLibBucket(Bucket inner)
            : this(inner, BucketCompressionAlgorithm.ZLib)
        {
        }

        public ZLibBucket(Bucket inner, BucketCompressionAlgorithm algorithm = BucketCompressionAlgorithm.ZLib, CompressionMode mode = CompressionMode.Decompress, BucketCompressionLevel level = BucketCompressionLevel.Default, int bufferSize = DefaultBufferSize)
            : base(inner)
        {
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            else if (bufferSize < 64)
                bufferSize = 64;

            _z = new ZStream();
            if (mode == CompressionMode.Compress)
                _level = level;
            _algorithm = algorithm;

            switch (algorithm)
            {
                case BucketCompressionAlgorithm.Deflate:
                case BucketCompressionAlgorithm.ZLib:
                    break;
                case BucketCompressionAlgorithm.GZip:
                    if (mode == CompressionMode.Compress)
                        throw new NotImplementedException();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(algorithm));
            }

            ZSetup();
            _z.NextOut = write_data = System.Buffers.ArrayPool<byte>.Shared.Rent(bufferSize); // Rents buffer of at least bufferSize
            _innerPoll = inner as IBucketPoll;
        }

        public override string Name => $"ZLib[{_algorithm}{(_level is null ? "Compress" : "Decompress")}]>{Inner.Name}";

        void ZSetup()
        {
            if (!_level.HasValue)
                _z.InflateInit(_algorithm == BucketCompressionAlgorithm.ZLib ? 15 : -15);
            else
                _z.DeflateInit((int)_level.Value, _algorithm == BucketCompressionAlgorithm.ZLib ? 15 : -15);

            if (_algorithm == BucketCompressionAlgorithm.GZip && !_level.HasValue)
                _headerLeft = 10;

            _eof = _readEof = false;
            read_buffer = BucketBytes.Empty;
            write_buffer = BucketBytes.Empty;
            _position = 0;
            _z.NextOutIndex = _z.NextInIndex = 0;
        }

        async ValueTask<bool> Refill(bool forPeek, int requested = int.MaxValue)
        {
            bool retry_refill;
            do
            {
                retry_refill = false;
                int to_read = 0;

                if (_headerLeft.HasValue && _headerLeft != 0)
                {
                    int left = Math.Abs(_headerLeft.Value);
                    while (left > 0)
                    {
                        if (forPeek)
                            return false;

                        var bb = await Inner.ReadAsync(left).ConfigureAwait(false);

                        if (bb.IsEof)
                        {
                            if (_algorithm == BucketCompressionAlgorithm.GZip && _headerLeft == 10)
                            {
                                _eof = true;
                                return true;
                            }
                            throw new InvalidOperationException($"Unexpected EOF in GZip header on {Inner.Name}");
                        }

                        int n = bb.Length;
                        if (n > 0)
                        {
                            left -= n;
                        }
                    }

                    if (_headerLeft > 0)
                        _headerLeft = 0;
                    else
                    {
                        _eof = true;
                        _headerLeft = null;
                        return true;
                    }
                }


                if (!_readEof && read_buffer.IsEmpty)
                {
                    var bb = ((_innerPoll is null) ? Inner.Peek() : await _innerPoll.PollAsync().ConfigureAwait(false));

                    if (bb.IsEmpty)
                    {
                        if (forPeek)
                            return false; // Not at EOF, not filled

                        bb = await Inner.ReadAsync(1).ConfigureAwait(false);

                        if (bb.Length == 0)
                        {
                            System.Diagnostics.Debug.Assert(bb.IsEof);
                            _readEof = true;
                            read_buffer = Array.Empty<byte>();
                        }
                        else
                        {
                            read_buffer = bb;
                            to_read = -1;

                            // We read one byte, and that might be the first byte of a new huge peek buffer
                            // Let's check if this first byte is just that...

                            byte bOne = bb[0];
                            var peek = Inner.Peek();

                            if (peek.IsEmpty)
                            {
                                // Too bad, we are probably at eof.
                                read_buffer = new byte[] { bOne };
                            }
                            else
                            {
                                var (tb, offs) = peek;

                                if (tb is not null && offs > 0 && tb[offs - 1] == bOne)
                                {
                                    // Nice guess. The peek buffer contains the read byte
                                    read_buffer = new BucketBytes(tb, offs - 1, peek.Length + 1);
                                }
                                else if (tb is not null)
                                {
                                    // Bad case, the read byte is not in the buffer.
                                    // Let's create something else

                                    byte[] buf = new byte[Math.Min(64, 1 + peek.Length)];
                                    buf[0] = bOne;
                                    for (int i = 1; i < buf.Length; i++)
                                        buf[i] = peek[i - 1];

                                    read_buffer = buf;
                                }
                                else
                                {
                                    // Auch, we got a span backed by something else than an array
                                    read_buffer = new byte[] { bOne };
                                }
                            }
                        }
                    }
                    else
                    {
                        read_buffer = bb;
                        to_read = 0;
                    }
                }

                var (rb, rb_offs) = read_buffer.ExpandToArray();

                _z.NextIn = rb;
                _z.NextInIndex = rb_offs;
                _z.AvailIn = read_buffer.Length;

                int wb_start;
                if (_z.NextOutIndex > 0 && requested < (write_data.Length - _z.NextOutIndex))
                {
                    wb_start = _z.NextOutIndex;
                }
                else
                {
                    _z.NextOutIndex = wb_start = 0;
                }

                _z.AvailOut = Math.Min(write_data.Length - wb_start, requested);

                int r;
                if (!_level.HasValue)
                    r = _z.Inflate(_readEof ? ZlibConst.ZFINISH : ZlibConst.ZSYNCFLUSH); // Write as much inflated data as possible
                else
                    r = _z.Deflate(_readEof ? ZlibConst.ZFINISH : ZlibConst.ZSYNCFLUSH);

                write_buffer = new BucketBytes(write_data, wb_start, _z.NextOutIndex - wb_start);
                _position += write_buffer.Length;

                if (r == ZlibConst.ZSTREAMEND)
                {
                    _readEof = true;

                    if (_headerLeft.HasValue)
                    {
                        _headerLeft = -8;
                    }
                    else
                        _eof = true;
                }
                else if (r == ZlibConst.ZBUFERROR && _readEof && _algorithm == BucketCompressionAlgorithm.Deflate && _z.NextOutIndex == 0)
                {
                    // Deflate decompression reports error at EOF. Appears to be fixed in ZLib itself.
                    // Covered by WrapTests.TestConvert() test over Deflate.
                    _eof = true;
                }
                else if (r != ZlibConst.ZOK)
                {
                    throw new BucketException($"ZLib handler failed {r}: {_z.Msg} on {Name} Bucket");
                }

                if (write_buffer.IsEmpty)
                    retry_refill = true;

                to_read += _z.NextInIndex - rb_offs;

                if (to_read > 0)
                {
                    // We peeked more data than what we read
                    read_buffer = BucketBytes.Empty; // Need to re-peek next time

                    var now_read = await Inner.ReadAsync(to_read).ConfigureAwait(false);
                    if (now_read.Length != to_read)
                        throw new BucketException($"Read on {Inner.Name} did not complete as promised by peek");


                }
                else
                {
                    if (to_read < 0)
                        read_buffer = read_buffer.Slice(0, -to_read);
                    else
                        read_buffer = read_buffer.Slice(_z.NextInIndex - rb_offs);
                }
            }
            while (retry_refill && !_eof);

            return _eof && write_buffer.IsEmpty;
        }

        public override BucketBytes Peek()
        {
            return write_buffer;
        }

        async ValueTask<BucketBytes> IBucketPoll.PollAsync(int minSize)
        {
            if (!_eof && write_buffer.IsEmpty)
                await Refill(false).ConfigureAwait(false);

            return write_buffer;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (requested <= 0)
                throw new ArgumentOutOfRangeException(nameof(requested));

            if (write_buffer.IsEmpty)
            {
                // Loosing data. See TestConvertFail for deflate and zlib stream
                int rq = (_level.HasValue && requested < 256) ? 256 : requested;

                if (_eof || await Refill(false, rq).ConfigureAwait(false))
                    return BucketBytes.Eof;
            }

            if (requested > write_buffer.Length)
                requested = write_buffer.Length;

            var bb = write_buffer.Slice(0, requested);
            write_buffer = write_buffer.Slice(requested);

            return bb;
        }

        async ValueTask IBucketSeek.SeekAsync(long newPosition)
        {
            if (newPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(newPosition));

            if (newPosition < Position)
            {
                if (_position - newPosition <= _z.NextOutIndex)
                {
                    // The info is still in the zlib buffer. We can just fix up the position in the buffer

                    int moveBack = (int)(_position - newPosition);
                    write_buffer = new BucketBytes(write_data, _z.NextOutIndex - moveBack, moveBack);
                    return;
                }

                // Reset the world and start reading at the start
                Reset();
            }

            long pos = Position!.Value;
            if (newPosition > pos)
            {
                await ReadSkipAsync(newPosition - pos).ConfigureAwait(false);
            }
        }

        public override bool CanReset => Inner.CanReset;

        public override long? Position => _position - write_buffer.Length;

        public override void Reset()
        {
            Inner.Reset();

            ZSetup();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (write_data != null)
                        System.Buffers.ArrayPool<byte>.Shared.Return(write_data);

                    write_data = null!;
                    read_buffer = default;
                    write_buffer = default;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override Bucket Duplicate(bool reset = false)
        {
            if (!reset)
                throw new InvalidOperationException();

            var b = Inner.Duplicate(reset);

            return new ZLibBucket(b, _algorithm, (_level is null) ? CompressionMode.Decompress : CompressionMode.Compress, _level ?? BucketCompressionLevel.Default, Math.Min(8192, write_data.Length));
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string DebuggerDisplay => read_buffer.AsDebuggerDisplay();
    }
}
