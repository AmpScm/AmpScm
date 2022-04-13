using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    internal class TextNormalizeBucket : WrappingBucket
    {
        Encoding _default;
        State _state;
        Bucket _inner;
        long _position;

        enum State
        {
            Done,
            Init,
            HighScan,
            EF,
            EFBB,
            FE,

        }

        public TextNormalizeBucket(Bucket inner)
            : base(inner)
        {
            _state = State.Init;
            _inner = inner;
            _default = ANSI;
        }

        public TextNormalizeBucket(Bucket inner, Encoding defaultEncoding)
            : this(inner)
        {
            _default = defaultEncoding ?? ANSI;
        }

        public override BucketBytes Peek()
        {
            if (_state == State.Done)
                return _inner.Peek();
            else
                return BucketBytes.Empty;
        }

        static Encoding ANSI { get; } = (Encoding.Default is UTF8Encoding) ? Encoding.GetEncoding("ISO-8859-1") : Encoding.Default;

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            BucketBytes bb;
            switch (_state)
            {
                case State.Done:
                    bb = await _inner.ReadAsync(requested).ConfigureAwait(false);
                    _position += bb.Length;
                    return bb;

                case State.HighScan:
                    bb = await _inner.ReadAsync(requested).ConfigureAwait(false);
                    _position += bb.Length;
                    await HighScan().ConfigureAwait(false);
                    return bb;

                case State.Init:
                    bb = Inner.Peek();

                    if (bb.Length >= 2)
                    {
                        switch ((bb[0], bb[1]))
                        {
                            case (0xEF, 0xBB) when (bb.Length > 3 && bb[2] == 0xBF):
                                bb = await Inner.ReadAsync(requested + 3).ConfigureAwait(false);
                                if (bb.Length <= 3)
                                    return BucketBytes.Eof;
                                _state = State.Done;
                                _position = bb.Length;
                                return bb.Slice(3);
                            case (0xFF, 0xFE):
                            case (0xFE, 0xFF):
                                _inner = new TextEncodingToUtf8Bucket(Inner,
                                    bb[0] == 0xFF ? Encoding.Unicode : Encoding.BigEndianUnicode);

                                bb = await Inner.ReadAsync(2).ConfigureAwait(false);
                                if (bb.Length != 2)
                                    throw new BucketException($"Unexpected EOF in {Name}");
                                _state = State.Done;

                                bb = await _inner.ReadAsync(requested).ConfigureAwait(false);
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

                            if (maybeUtf8 >= 0 && bb.Length > i + 1 && (bb[i + 1] & 0xB0) == 0x80)
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
                                for (int j = 1; j <= n && j < bb.Length - i; j++)
                                {
                                    if ((bb[i + j] & 0xB0) != 0x80)
                                    {
                                        utf8Valid = false;
                                        break;
                                    }
                                }

                                if (utf8Valid)
                                {
                                    maybeUtf8++;
                                    i += n;
                                }
                                else
                                    maybeUtf8 = -1;
                            }
                            else if (bb.Length >= i+1)
                                maybeUtf8--;
                        }

                        if (maybeUtf8 > 0)
                        {
                            _state = State.Done;
                            _inner = Inner;
                            goto case State.Done;
                        }
                        else if (maybeUnicode > 2 && maybeUnicode > bb.Length /8)
                        {
                            _state = State.Done;
                            _inner = new TextEncodingToUtf8Bucket(Inner,
                                    odd >= (maybeUnicode/2) ? Encoding.Unicode : Encoding.BigEndianUnicode);
                            goto case State.Done;
                        }
                        else if (maybeUtf8 < 0)
                        {
                            _state = State.Done;
                            _inner = new TextEncodingToUtf8Bucket(Inner, _default);
                            goto case State.Done;
                        }
                    }

                    bb = await Inner.ReadAsync(requested).ConfigureAwait(false);
                    if (bb.IsEof)
                    {
                        _state = State.Done;
                        return bb;
                    }
                    else if (bb[0] == 0xEF)
                    {
                        if (bb.Length >= 3 && bb[1] == 0xBB && bb[2] == 0xBF)
                        {
                            _inner = Inner;
                            _state = State.Done;
                            _position = bb.Length - 3;

                            if (bb.Length > 3)
                                return bb.Slice(3);
                            else
                                goto case State.Done;
                        }
                        else if (bb.Length == 2 && bb[1] == 0xBB)
                        {
                            var bb2 = await Inner.ReadAsync(requested).ConfigureAwait(false);

                            if (bb2.Length > 0 && bb2[0] == 0xBF)
                            {
                                _inner = Inner;
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
                                    _inner = Inner;
                                    _position = bb.Length + bb2.Length;
                                    _state = State.Done;
                                    return bb.ToArray().Concat(bb2.ToArray()).ToArray();
                                }
                            }
                        }
                        else if (bb.Length == 1)
                        {
                            var bb2 = await Inner.ReadAsync(Math.Max(2, requested-1)).ConfigureAwait(false);

                            if (bb2.Length >= 2 && bb2[0] == 0xBB && bb2[1] == 0xBF)
                            {
                                _inner = Inner;
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
                                _inner = new TextEncodingToUtf8Bucket(new byte[] { 0xEF }.AsBucket() + bb2.ToArray().AsBucket() + Inner, _default);
                                goto case State.Done;
                            }
                            else if (bb2.IsEof)
                            {
                                _inner = Inner;
                                _state = State.Done;
                                return new byte[] { 0xEF };
                            }

                            bb2 = await Inner.ReadAsync(Math.Max(1, requested - 2)).ConfigureAwait(false);

                            if (bb2.IsEof)
                            {
                                _inner = Inner;
                                _state = State.Done;
                                return new byte[] { 0xEF, 0xBB };
                            }
                            else if (bb2[0] == 0xBF)
                            {
                                _inner = Inner;
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
                            bck = bb.Slice(2).ToArray().AsBucket() + Inner;
                        else
                            bck = Inner;

                        _inner = new TextEncodingToUtf8Bucket(bck,
                                bb[0] == 0xFF ? Encoding.Unicode : Encoding.BigEndianUnicode);
                        _state = State.Done;

                        bb = await _inner.ReadAsync(requested).ConfigureAwait(false);
                        _position = bb.Length;
                        return bb;
                    }
                    _inner = Inner;
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

                    if (bb.Length > i + 1 && (bb[i + 1] & 0xB0) == 0x80)
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
                        for (int j = 1; j <= n && j < bb.Length - i; j++)
                        {
                            if ((bb[i + j] & 0xB0) != 0x80)
                            {
                                utf8Valid = false;
                                break;
                            }
                        }

                        if (utf8Valid)
                        {
                            maybeUtf8++;
                        }
                        else
                        {
                            maybeUtf8 = -1;
                            break;
                        }
                    }
                }

                if (maybeUtf8 >= 1)
                {
                    _state = State.Done;
                    _inner = Inner;
                }
                else if (maybeUtf8 < 0)
                {
                    _position -= bb.Length;
                    _inner = new TextEncodingToUtf8Bucket(bb.ToArray().AsBucket() + _inner, _default);
                    bb = await _inner.ReadAsync(requested).ConfigureAwait(false);
                    _position += bb.Length;
                }
            }            
        }

        public override long? Position => _position;
    }
}
