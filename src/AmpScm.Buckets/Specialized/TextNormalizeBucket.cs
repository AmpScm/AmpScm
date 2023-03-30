using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class TextNormalizeBucket : WrappingBucket, IBucketPoll
    {
        private readonly Encoding _default;
        private State _state;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private Bucket _source;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private long _position;

        private enum State
        {
            Done,
            Init,
            HighScan,
            EF,
            EFBB,
            FE,

        }

        public TextNormalizeBucket(Bucket source)
            : base(source)
        {
            _state = State.Init;
            _source = source;
            _default = DefaultEncoding;
        }

        public TextNormalizeBucket(Bucket source, Encoding fallbackEncoding)
            : this(source)
        {
            _default = fallbackEncoding ?? DefaultEncoding;
        }

        public override BucketBytes Peek()
        {
            if (_state == State.Done)
                return _source.Peek();
            else
                return BucketBytes.Empty;
        }

        public ValueTask<BucketBytes> PollAsync(int minRequested = 1)
        {
            if (_state == State.Done)
                return _source.PollAsync(minRequested);
            else
                return new (BucketBytes.Empty);
        }

        public static Encoding DefaultEncoding { get; } = (Encoding.Default is UTF8Encoding)
#if NET5_0_OR_GREATER
            ? Encoding.Latin1
#else
            ? Encoding.GetEncoding("ISO-8859-1")
#endif
            : Encoding.Default;

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            BucketBytes bb;
            switch (_state)
            {
                case State.Done:
                    bb = await _source.ReadAsync(requested).ConfigureAwait(false);
                    _position += bb.Length;
                    return bb;

                case State.HighScan:
                    bb = await _source.ReadAsync(requested).ConfigureAwait(false);
                    _position += bb.Length;
                    await HighScan().ConfigureAwait(false);
                    return bb;

                case State.Init:
                    bb = Source.Peek();

                    if (bb.Length >= 2)
                    {
                        switch ((bb[0], bb[1]))
                        {
                            case (0xEF, 0xBB):
                                bb = BucketBytes.Empty; // No type scan. Use prefix scan
                                break;

                            case (0xFF, 0xFE):
                            case (0xFE, 0xFF):
                                _source = new TextRecoderBucket(Source,
                                    bb[0] == 0xFF ? Encoding.Unicode : Encoding.BigEndianUnicode);

                                bb = await Source.ReadAsync(2).ConfigureAwait(false);
                                if (bb.Length != 2)
                                    throw new BucketEofException(Source);
                                _state = State.Done;

                                bb = await _source.ReadAsync(requested).ConfigureAwait(false);
                                _position = bb.Length;
                                return bb;
                        }

                        int maybeUnicode = 0;
                        int maybeUtf8 = 0;
                        int odd = 0;
                        for (int i = 0; i < bb.Length; i++)
                        {
                            if (bb[i] == 0)
                            {
                                maybeUnicode++;
                                if ((i & 1) != 0)
                                    odd++;
                            }

                            if (bb[i] < 0x80)
                                continue;

                            if ((bb[i] & 0xC0) != 0xC0)
                                maybeUtf8 = -1;
                            else if (maybeUtf8 >= 0 && bb.Length > i + 1 && (bb[i + 1] & 0xC0) == 0x80)
                            {
                                // We most likely have UTF-8. Check a few more cases if possible
                                byte fcp = bb[i];
                                int n = 0;
                                while (0 != (fcp & 0x80) && n < 6)
                                {
                                    n++;
                                    fcp <<= 1;
                                }

                                bool utf8Valid = true;
                                for (int j = 1; j < n && j < bb.Length - i; j++)
                                {
                                    if ((bb[i + j] & 0xC0) != 0x80)
                                    {
                                        utf8Valid = false;
                                        break;
                                    }
                                }

                                if (utf8Valid)
                                {
                                    maybeUtf8++;
                                    i += n - 1;
                                }
                                else
                                    maybeUtf8 = -1;
                            }
                            else if (bb.Length > i + 1)
                                maybeUtf8--;
                        }

                        if (maybeUtf8 > 0)
                        {
                            _state = State.Done;
                            _source = Source;
                            goto case State.Done;
                        }
                        else if (maybeUnicode > 2 && maybeUnicode > bb.Length / 8)
                        {
                            _state = State.Done;
                            _source = new TextRecoderBucket(Source,
                                    odd >= Math.Max(2, (maybeUnicode / 4)) ? Encoding.Unicode : Encoding.BigEndianUnicode);
                            goto case State.Done;
                        }
                        else if (maybeUtf8 < 0)
                        {
                            _state = State.Done;
                            _source = new TextRecoderBucket(Source, _default);
                            goto case State.Done;
                        }
                    }

                    bb = await Source.ReadAsync(requested).ConfigureAwait(false);
                    if (bb.IsEof)
                    {
                        _state = State.Done;
                        return bb;
                    }
                    else if (bb[0] == 0xEF)
                    {
                        if (bb.Length >= 3 && bb[1] == 0xBB && bb[2] == 0xBF)
                        {
                            _source = Source;
                            _state = State.Done;
                            _position = bb.Length - 3;

                            if (bb.Length > 3)
                                return bb.Slice(3);
                            else
                                goto case State.Done;
                        }
                        else if (bb.Length == 2 && bb[1] == 0xBB)
                        {
                            var bb2 = await Source.ReadAsync(requested).ConfigureAwait(false);

                            if (bb2.Length > 0 && bb2[0] == 0xBF)
                            {
                                _source = Source;
                                _state = State.Done;
                                _position = bb2.Length - 1;

                                if (bb2.Length > 1)
                                    return bb2.Slice(1);
                                else
                                    goto case State.Done;
                            }
                            else
                            {
                                if (requested >= bb.Length + bb2.Length)
                                {
                                    _source = Source;
                                    _position = bb.Length + bb2.Length;
                                    _state = State.Done;
                                    return new byte[] { 0xEF, 0xBB }.AppendBytes(bb2);
                                }
                            }
                        }
                        else if (bb.Length == 1)
                        {
                            var bb2 = await Source.ReadAsync(Math.Max(2, requested - 1)).ConfigureAwait(false);

                            if (bb2.Length >= 2 && bb2[0] == 0xBB && bb2[1] == 0xBF)
                            {
                                _source = Source;
                                _state = State.Done;
                                _position = bb2.Length - 2;

                                if (bb2.Length > 2)
                                    return bb2.Slice(2);
                                else
                                    goto case State.Done;
                            }
                            else if (bb2.Length >= 1 && bb2[0] != 0xBB || (bb2.Length >= 2 && bb2[1] != 0xBF))
                            {
                                // We don't have UTF-8
                                _position = 0;
                                _state = State.Done;
                                _source = new TextRecoderBucket(new byte[] { 0xEF }.AsBucket() + bb2.ToArray().AsBucket() + Source, _default);
                                goto case State.Done;
                            }
                            else if (bb2.IsEof)
                            {
                                _source = Source;
                                _state = State.Done;
                                return new byte[] { 0xEF };
                            }

                            bb2 = await Source.ReadAsync(Math.Max(1, requested - 2)).ConfigureAwait(false);

                            if (bb2.IsEof)
                            {
                                _source = Source;
                                _state = State.Done;
                                return new byte[] { 0xEF, 0xBB };
                            }
                            else if (bb2[0] == 0xBF)
                            {
                                _source = Source;
                                _state = State.Done;

                                if (bb2.Length > 1)
                                {
                                    _position = bb2.Length - 1;
                                    return bb2.Slice(1);
                                }
                                else
                                    goto case State.Done;
                            }
                        }
                    }
                    else if ((bb[0] == 0xFF && bb.Length > 1 && bb[1] == 0xFE)
                            || (bb[0] == 0xFE && bb.Length > 1 && bb[1] == 0xFF))
                    {
                        Bucket bck;
                        if (bb.Length > 2)
                            bck = bb.Slice(2).ToArray().AsBucket() + Source;
                        else
                            bck = Source;

                        _source = new TextRecoderBucket(bck,
                                bb[0] == 0xFF ? Encoding.Unicode : Encoding.BigEndianUnicode);
                        _state = State.Done;

                        bb = await _source.ReadAsync(requested).ConfigureAwait(false);
                        _position = bb.Length;
                        return bb;
                    }
                    else if (bb.Length == 1 && (bb[0] == 0xFF || bb[0] == 0xFE))
                    {
                        byte b0 = bb[0];
                        bb = await Source.ReadAsync(1).ConfigureAwait(false);

                        if (bb.Length == 1 && (bb[0] == 0xFF || bb[0] == 0xFE) && bb[0] != b0)
                        {
                            _source = new TextRecoderBucket(Source,
                                b0 == 0xFF ? Encoding.Unicode : Encoding.BigEndianUnicode);
                            _state = State.Done;
                            goto case State.Done;
                        }
                        else if (bb.IsEof)
                            return new[] { b0 };
                        else
                        {
                            bb = new[] { b0, bb[0] };
                        }
                    }
                    _source = Source;
                    _state = State.HighScan;
                    _position = bb.Length;
                    await HighScan().ConfigureAwait(false);
                    return bb;

                default:
                    throw new InvalidOperationException();
            }

            async ValueTask HighScan()
            {
                int maybeUtf8 = 0;
                for (int i = 0; i < bb.Length; i++)
                {
                    if (bb[i] < 0x80)
                        continue;

                    if ((bb[i] & 0xC0) != 0xC0)
                        maybeUtf8 = -1;
                    else if (bb.Length > i + 1 && (bb[i + 1] & 0xC0) == 0x80)
                    {
                        // We most likely have UTF-8. Check a few more if possible
                        byte fcp = bb[i];
                        int n = 0;
                        while (0 != (fcp & 0x80) && n < 6)
                        {
                            n++;
                            fcp <<= 1;
                        }

                        bool utf8Valid = true;
                        for (int j = 1; j < n && j < bb.Length - i; j++)
                        {
                            if ((bb[i + j] & 0xC0) != 0x80)
                            {
                                utf8Valid = false;
                                break;
                            }
                        }

                        if (utf8Valid)
                        {
                            maybeUtf8++;
                            i += n - 1;
                        }
                        else
                        {
                            maybeUtf8 = -1;
                            break;
                        }
                    }
                    else if (i == bb.Length - 1 && maybeUtf8 == 0)
                    {
                        byte[] a = bb.ToArray();

                        if (await Source.ReadByteAsync().ConfigureAwait(false) is byte b)
                        {
                            bb = a.ArrayAppend(b);
                            i--;
                        }
                        else
                            break;
                    }
                }

                if (maybeUtf8 >= 1)
                {
                    _state = State.Done;
                    _source = Source;
                }
                else if (maybeUtf8 < 0)
                {
                    _position -= bb.Length;
                    _source = new TextRecoderBucket(bb.ToArray().AsBucket() + _source, _default);
                    bb = await _source.ReadAsync(requested).ConfigureAwait(false);
                    _position += bb.Length;
                }
            }
        }

        public override long? Position => _position;

        public override bool CanReset => Source.CanReset;

        public override void Reset()
        {
            base.Reset();
            _state = State.Init;
            _position = 0;
            _source = base.Source;
        }

        public override Bucket Duplicate(bool reset = false)
        {
            if (!reset)
                throw new NotSupportedException();

            var b = Source.Duplicate(reset);
            return new TextNormalizeBucket(b, _default);
        }
    }
}
