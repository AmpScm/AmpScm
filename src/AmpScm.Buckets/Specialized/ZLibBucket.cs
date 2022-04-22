using System;
using System.IO.Compression;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using Elskom.Generic.Libs;

namespace AmpScm.Buckets.Specialized
{
    public enum ZLibLevel
    {
        Store = ZlibConst.ZNOCOMPRESSION,
        BestSpeed = ZlibConst.ZBESTSPEED,
        Maximum = ZlibConst.ZBESTCOMPRESSION
    }
    public sealed class ZLibBucket : WrappingBucket, IBucketPoll, IBucketSeek
    {
        readonly ZStream _z;
        bool _eof, _readEof;
        BucketBytes read_buffer;
        BucketBytes write_buffer;
        byte[] write_data;
        long _position;
        int? _windowBits;
        ZLibLevel? _level;
        readonly IBucketPoll? innerPoll;
        int? _headerLeft;

        public ZLibBucket(Bucket inner)
            : this(inner, 15 /* 15 for zlib. -15 for deflate and 31 for gzip */)
        {
            innerPoll = inner as IBucketPoll;
        }

        private ZLibBucket(Bucket inner, int windowBits)
            : base(inner)
        {
            _z = new();
            _z.InflateInit(windowBits);
            write_data = new byte[8192];
            _windowBits = windowBits;
        }

        public ZLibBucket(Bucket inner, ZLibLevel level)
            : base(inner)
        {
            _z = new ZStream();
            _z.DeflateInit((int)level);
            write_data = new byte[8192];
            _level = level;
        }

        internal ZLibBucket(Bucket inner, BucketCompressionAlgorithm zlibAlgorithm, CompressionMode mode)
            : base(inner)
        {
            _z = new ZStream();
            switch ((zlibAlgorithm, mode))
            {
                case (BucketCompressionAlgorithm.ZLib, CompressionMode.Decompress):
                    _windowBits = 15;
                    _z.InflateInit(_windowBits.Value);
                    break;
                case (BucketCompressionAlgorithm.ZLib, CompressionMode.Compress):
                    _level = (ZLibLevel)ZlibConst.ZDEFAULTCOMPRESSION;
                    _windowBits = 15;
                    _z.DeflateInit(ZlibConst.ZBESTCOMPRESSION, _windowBits.Value);
                    break;
                case (BucketCompressionAlgorithm.Deflate, CompressionMode.Decompress):
                    _windowBits = -15;
                    _z.InflateInit(_windowBits.Value);
                    break;
                case (BucketCompressionAlgorithm.Deflate, CompressionMode.Compress):
                    _level = (ZLibLevel)ZlibConst.ZDEFAULTCOMPRESSION;
                    _windowBits = -15;
                    _z.DeflateInit(ZlibConst.ZBESTCOMPRESSION, _windowBits.Value);
                    break;
                case (BucketCompressionAlgorithm.GZip, CompressionMode.Decompress):
                    _windowBits = -15;
                    _z.InflateInit(_windowBits.Value);
                    _headerLeft = 10;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(zlibAlgorithm));
            }
            write_data = new byte[8192];
        }

        public override string Name => "ZLib>" + Inner.Name;

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
                            if (_windowBits == -15 && _headerLeft == 10)
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
                    var bb = ((innerPoll is null) ? Inner.Peek() : await innerPoll.PollAsync().ConfigureAwait(false));

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

                _z.NextOut = write_data;
                _z.NextOutIndex = 0;
                _z.AvailOut = Math.Min(write_data.Length, requested);

                int r;
                if (!_level.HasValue)
                    r = _z.Inflate(_readEof ? ZlibConst.ZFINISH : ZlibConst.ZSYNCFLUSH); // Write as much inflated data as possible
                else
                    r = _z.Deflate(_readEof ? ZlibConst.ZFINISH : ZlibConst.ZSYNCFLUSH);

                write_buffer = new BucketBytes(write_data, 0, _z.NextOutIndex);

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
                //else if (r == zlibConst.Z_BUF_ERROR && _readEof && _z.next_out_index == 0)
                //{
                //    _eof = true;
                //}
                else if (r != ZlibConst.ZOK)
                {
                    throw new System.IO.IOException($"ZLib handler failed {r}: {_z.Msg}");
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
                        throw new BucketException($"Read on {Inner.Name} did not complete as promissed by peek");
                }
                else
                    read_buffer = read_buffer.Slice(_z.NextInIndex - rb_offs);
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
                if (_eof || await Refill(false, requested).ConfigureAwait(false))
                    return BucketBytes.Eof;
            }

            if (requested > write_buffer.Length)
                requested = write_buffer.Length;

            var bb = write_buffer.Slice(0, requested);
            write_buffer = write_buffer.Slice(requested);
            _position += requested;

            System.Diagnostics.Debug.Assert(bb.Length > 0);

            return bb;
        }

        async ValueTask IBucketSeek.SeekAsync(long newPosition)
        {
            if (newPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(newPosition));

            if (newPosition < _position)
            {
                var (b, i) = write_buffer;

                if (b is not null && _position - newPosition < i)
                {
                    // We are very lucky. We still have this in our output buffer :-)
                    int moveBack = (int)(_position - newPosition);
                    write_buffer = new BucketBytes(b, i - moveBack, write_buffer.Length + moveBack);
                    _position -= moveBack;
                }
                else
                {
                    // Reset the world and start reading at the start
                    await Inner.ResetAsync().ConfigureAwait(false);

                    if (_windowBits is int wb)
                        _z.InflateInit(wb);
                    else if (_level.HasValue)
                        _z.DeflateInit((int)_level);

                    _eof = _readEof = false;
                    read_buffer = BucketBytes.Empty;
                    write_buffer = BucketBytes.Empty;
                    _position = 0;
                }
            }

            if (newPosition > _position)
            {
                await ReadSkipAsync(newPosition-_position).ConfigureAwait(false);
            }
        }

        public override bool CanReset => Inner.CanReset;

        public override long? Position => _position;

        public override async ValueTask ResetAsync()
        {
            if (!CanReset)
                throw new InvalidOperationException();

            await Inner.ResetAsync().ConfigureAwait(false);

            if (_windowBits is int wb)
                _z.InflateInit(wb);
            else if (_level.HasValue)
                _z.DeflateInit((int)_level);

            _eof = _readEof = false;
            read_buffer = BucketBytes.Empty;
            write_buffer = BucketBytes.Empty;
            _position = 0;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                write_data = null!;
                read_buffer = default;
                write_buffer = default;
            }
        }

        public override async ValueTask<Bucket> DuplicateAsync(bool reset)
        {
            if (!reset)
                throw new InvalidOperationException();

            var b = await Inner.DuplicateAsync(reset).ConfigureAwait(false);

            if (_windowBits.HasValue)
                return new ZLibBucket(b, _windowBits.Value);
            else if (_level.HasValue)
                return new ZLibBucket(b, _level.Value);
            else
                throw new InvalidOperationException();
        }
    }
}
