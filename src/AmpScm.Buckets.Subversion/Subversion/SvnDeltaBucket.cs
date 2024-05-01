using System.Buffers;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Subversion;

public class SvnDeltaBucket : SvnBucket
{
    private Bucket DeltaBase { get; }

    private byte _version;
    private readonly bool _haveBase;
    private BucketBytes _remaining;
    private long _position;
    private ReadOnlyMemory<byte> _srcView;
    private long _srcViewoffset;
    private byte[]? _window;
    private Action? _atEof;

    public SvnDeltaBucket(Bucket source, Bucket? deltaBase, Action? atEof = null)
        : base(source)
    {
        DeltaBase = deltaBase ?? Empty;
        _version = 0xFF;

        _haveBase = (deltaBase != null);
        _atEof = atEof;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var rw = _window;
            _window = null;

            if (rw != null)
            {
                ArrayPool<byte>.Shared.Return(rw);
            }
        }
        base.Dispose(disposing);
    }

    private async ValueTask<int> ReadVersionAsync()
    {
        if (_version != 0xFF)
            return _version;

        var bb = await Source.ReadExactlyAsync(4).ConfigureAwait(false);

        if (bb.Length != 4)
            throw new BucketEofException(Source);

        if (!bb.StartsWithASCII("SVN"))
            throw new BucketException("Not a valid delta header");

        _version = bb[3];

        if (_version >= 3)
            throw new BucketException("Only SVNDIFF 0-2 are supported");

        return _version;
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        if (!_remaining.IsEmpty)
            return BucketBytes.PartialReturn(ref _remaining, requested);
        else if (_remaining.IsEof)
            return _remaining;

        await ReadNextWindow().ConfigureAwait(false);

        var bb = BucketBytes.PartialReturn(ref _remaining, requested);

        if (bb.IsEof)
        {
            var ae = _atEof;
            _atEof = null;
            ae?.Invoke();
        }
        else
        {
            _position += bb.Length;
        }

        return bb;
    }

    private async ValueTask ReadNextWindow()
    {
        if (_version == 0xFF)
            await ReadVersionAsync().ConfigureAwait(false);

        var bb = await Source.PollAsync().ConfigureAwait(false);

        if (bb.StartsWithASCII("ENDREP\n"))
        {
            _remaining = BucketBytes.Eof;
            return;
        }

        long sview_offset = await ReadLongLengthAsync(Source).ConfigureAwait(false);
        int sview_len = await ReadLengthAsync(Source).ConfigureAwait(false);
        int tview_len = await ReadLengthAsync(Source).ConfigureAwait(false);
        int ilen = await ReadLengthAsync(Source).ConfigureAwait(false);
        int dlen = await ReadLengthAsync(Source).ConfigureAwait(false);
        int orig_ilen;


        if (tview_len > (_window?.Length ?? 0))
        {
            int len = (DeltaBase != null) ? Math.Max(tview_len, 1024) : tview_len;

            if (_window != null)
            {
                ArrayPool<byte>.Shared.Return(_window);
                _window = null;
            }

            _window = ArrayPool<byte>.Shared.Rent(len);
        }

        if (_version > 0)
        {
            orig_ilen = await ReadLengthAsync(Source).ConfigureAwait(false);
            ilen -= LengthOfLength(orig_ilen);
        }
        else
            orig_ilen = ilen;

        Memory<byte> instructions;
        if (ilen > 0)
            instructions = (await Source.ReadExactlyAsync(ilen).ConfigureAwait(false)).ToArray();
        else
            instructions = new();

        int orig_dlen;
        if (_version > 0)
        {
            orig_dlen = await ReadLengthAsync(Source).ConfigureAwait(false);
            dlen -= LengthOfLength(orig_dlen);
        }
        else
            orig_dlen = dlen;

        Memory<byte> data;
        if (dlen > 0)
        {
            data = (await Source.ReadExactlyAsync(dlen).ConfigureAwait(false)).ToArray();
        }
        else
            data = new();

        if (ilen != orig_ilen)
            Decode(ref instructions, orig_ilen);

        if (dlen != orig_dlen)
            Decode(ref data, orig_dlen);

        byte[] target = new byte[tview_len];

        var il = instructions.ToArray().AsBucket();

        int d_offset = 0;

        if (sview_len > 0)
        {
            if (!_srcView.IsEmpty && _srcViewoffset + _srcView.Length > sview_offset)
            {
                if (sview_len + sview_offset <= _srcViewoffset + _srcView.Length)
                {
                    _srcView = _srcView.Slice((int)(_srcViewoffset + _srcView.Length - (sview_len + sview_offset)));
                }
                else
                {
                    byte[] newView = new byte[sview_len];

                    int moveOver = (int)(_srcViewoffset + _srcView.Length - sview_offset);

                    _srcView.Slice(_srcView.Length - moveOver).CopyTo(newView);

                    bb = await DeltaBase!.ReadExactlyAsync(newView.Length - moveOver).ConfigureAwait(false);

                    bb.CopyTo(newView, moveOver);
                }
            }
            else
            {
                await DeltaBase!.SeekAsync(sview_offset).ConfigureAwait(false);
                bb = await DeltaBase!.ReadExactlyAsync((int)sview_len).ConfigureAwait(false);

                _srcView = bb;
                _srcViewoffset = sview_offset;
            }
        }

#if DEBUG
        Array.Clear(_window!, 0, _window!.Length);
#endif
        int opos = 0;
        while (await il.ReadByteAsync().ConfigureAwait(false) is byte b)
        {
            int op_len = b & 0x3F;

            if (op_len == 0)
            {
                op_len = checked((int)await ReadLengthAsync(il).ConfigureAwait(false));
            }

            switch (b >> 6)
            {
                case 0x00: // Copy from source
                    var offs = await ReadLengthAsync(il).ConfigureAwait(false);
                    _srcView.Slice(offs, op_len).Span.CopyTo(_window.AsSpan(opos));
                    opos += op_len;
                    // @a length bytes at @a offset from source view
                    break;
                case 0x01: // Copy from target
                    offs = await ReadLengthAsync(il).ConfigureAwait(false);
                    while (offs + op_len > opos)
                    {
                        if (opos == 0)
                            throw new InvalidOperationException();

                        int mv = opos - offs;
                        _window.AsMemory(offs, mv).CopyTo(_window.AsMemory(opos));
                        opos += mv;
                        op_len -= mv;
                    }
                    _window.AsMemory(offs, op_len).CopyTo(_window.AsMemory(opos));
                    opos += op_len;
                    break;
                case 0x02:
                    // Bytes from new data
                    data.Slice(d_offset, op_len).CopyTo(_window.AsMemory(opos));
                    opos += op_len;
                    d_offset += op_len;
                    break;
                case 0x03:
                    throw new InvalidOperationException();
            }

        }

        _remaining = _window.AsMemory(0, opos);
    }

    private void Decode(ref Memory<byte> data, int original_length)
    {
        switch (_version)
        {
            case 1 when (original_length < 512):
                break;
            case 1:
                data = new ZLibBucket(data.AsBucket(), BucketCompressionAlgorithm.Deflate, bufferSize: original_length).ToArray();
                break;
            case 2:
                byte[] result = new byte[original_length];
                K4os.Compression.LZ4.LZ4Codec.Decode(data.Span, result);
                data = result;
                break;
            default:
                throw new InvalidOperationException();
        }
    }

    private static int LengthOfLength(long value)
    {
        //#if !NETFRAMEWORK
        //            return (BitOperations.Log2((uint)Math.Min(value, 1)) + 6) / 7;
        //#else
        int n = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            n++;
        }

        return n;
        //#endif
    }

    private static async ValueTask<long> ReadLongLengthAsync(Bucket source)
    {
        ulong len = 0;
        byte b;

        do
        {
            b = await source.ReadByteAsync().ConfigureAwait(false) ?? throw new BucketEofException(source);

            len = (len << 7) | (uint)(b & 0x7F);
        }
        while (b >= 0x80);

        return checked((long)len);
    }

    private static async ValueTask<int> ReadLengthAsync(Bucket source)
    {
        long l = await ReadLongLengthAsync(source).ConfigureAwait(false);

        int i = (int)l;

        if (i != l)
            throw new BucketException();

        return i;
    }

    public override long? Position => _position;
}
