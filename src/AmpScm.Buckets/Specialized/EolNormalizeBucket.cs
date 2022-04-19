using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class EolNormalizeBucket : WrappingBucket
    {
        readonly BucketEol _acceptedEols;
        readonly BucketEol _producedEol;
        byte[] _eol;
        State _state;
        byte? _keep;
        enum State
        {
            Read,
            Eol0,
            Eol1,
            CRSplit
        }

        public EolNormalizeBucket(Bucket inner, BucketEol acceptedEols, BucketEol producedEol = BucketEol.LF)
            : base(inner)
        {
            if (0 != (acceptedEols & ~BucketEol.EolMask) || acceptedEols == BucketEol.None)
                throw new ArgumentOutOfRangeException(nameof(acceptedEols));
            else if (producedEol <= BucketEol.None || producedEol >= BucketEol.EolMask)
                throw new ArgumentOutOfRangeException(nameof(producedEol));

            _acceptedEols = acceptedEols;
            _producedEol = producedEol;
            _state = State.Read;

            _eol = producedEol switch
            {
                BucketEol.LF => new[] { (byte)'\n' },
                BucketEol.CRLF => new[] { (byte)'\r', (byte)'\n' },
                BucketEol.CR => new[] { (byte)'\r' },
                BucketEol.Zero => new[] { (byte)'\0' },
                _ => throw new ArgumentOutOfRangeException(nameof(producedEol))
            };
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            BucketBytes bb;
            BucketEol eol;
            BucketEol accepted = _acceptedEols;
            bool crMode = false;
            int rq = requested;

            switch (_state)
            {
                case State.Read:
                    if (_keep.HasValue)
                    {
                        if (_keep == '\0' && (_acceptedEols & BucketEol.Zero) != 0)
                        {
                            _keep = null;
                            goto case State.Eol0;
                        }
                        else if (_keep != '\r')
                        {
                            var r = new[] { _keep.Value };
                            _keep = null;
                            return r;
                        }
                        else
                        {
                            _keep = null;
                            _state = State.CRSplit;
                            goto case State.CRSplit;
                        }
                    }

                    (bb, eol) = await Inner.ReadUntilEolAsync(accepted, rq).ConfigureAwait(false);

                    if (eol == BucketEol.None)
                    {
                        if (!crMode)
                            return bb;
                        else if ((_acceptedEols & BucketEol.CR) != 0)
                        {
                            if (!bb.IsEof)
                                _keep = bb[0];
                            _state = State.Eol0;
                            goto case State.Eol0;
                        }
                        else if (requested > 1 || bb.IsEof)
                        {
                            return new[] { (byte)'\r', bb[0] };
                        }
                        else
                        {
                            _keep = bb[0];
                            return new[] { (byte)'\r' };
                        }
                    }
                    else if (0 != (eol & _acceptedEols))
                    {
                        if (eol == _producedEol)
                            return bb;
                        else
                        {
                            _state = State.Eol0;
                            bb = bb.Slice(0, bb.Length - eol.CharCount());
                            if (!bb.IsEmpty)
                                return bb;
                            else
                                goto case State.Eol0;
                        }
                    }
                    else if (eol == BucketEol.CRSplit)
                    {
                        _state = State.CRSplit;
                        bb = bb.Slice(0, bb.Length - eol.CharCount());
                        if (!bb.IsEmpty)
                            return bb;
                        else
                            goto case State.CRSplit;
                    }
                    else
                        throw new NotImplementedException();

                case State.Eol0:
                    if (requested >= _eol.Length)
                    {
                        _state = State.Read;
                        return _eol;
                    }
                    else
                    {
                        _state = State.Eol1;
                        return new byte[] { _eol[0] };
                    }

                case State.Eol1:
                    _state = State.Read;
                    return new byte[] { _eol[1] };

                case State.CRSplit:
                    {
                        bb = await Inner.ReadAsync(1).ConfigureAwait(false);

                        if (!bb.IsEof && bb[0] == (byte)'\n')
                        {
                            // We have a proper "\r\n"
                            _state = State.Eol0;
                            goto case State.Eol0;
                        }
                        else if (!bb.IsEof && bb[0] == (byte)'\r' && (_acceptedEols & BucketEol.CR) == 0)
                        {
                            // We have a "\r\r"
                            return bb; // And then do the same check again
                        }
                        else if ((_acceptedEols & BucketEol.CR) != 0)
                        {
                            if (!bb.IsEmpty)
                                _keep = bb[0]; // Keep for next read

                            _state = State.Eol0;
                            goto case State.Eol0;
                        }
                        else
                        {
                            _state = State.Read;

                            if (requested >= 2 && bb[0] != '\0')
                                return new[] { (byte)'\r', bb[0] };
                            else
                            {
                                _keep = bb[0];
                                return new[] { (byte)'\r' };
                            }
                        }
                    }

                default:
                    throw new InvalidOperationException();
            }
        }

        public override BucketBytes Peek()
        {
            switch (_state)
            {
                case State.Eol0:
                    return _eol;
                case State.Eol1:
                    return new byte[] { _eol[1] };
                case State.Read:
                    break;
                default:
                    return BucketBytes.Empty;
            }

            if (_keep.HasValue)
                return new[] { _keep.Value };

            var bb = Inner.Peek();

            if (bb.IsEmpty)
                return bb;

            bool checkLF = (_acceptedEols & BucketEol.LF) != 0;
            bool checkCR = (_acceptedEols & (BucketEol.CR | BucketEol.CRLF)) != 0;
            bool checkZero = (_acceptedEols & BucketEol.Zero) != 0;

            var span = bb.Span;
            var stop = (checkLF, checkCR, checkZero) switch
            {
                (true, false, false) => span.IndexOf((byte)'\n'),
                (false, true, false) => span.IndexOf((byte)'\r'),
                (false, false, true) => span.IndexOf((byte)'\0'),
                (true, true, false) => span.IndexOfAny((byte)'\r', (byte)'\n'),
                _ => span.IndexOfAny((byte)'\n', (byte)'\r', (byte)'\0')
            };

            if (stop > 0)
                return bb.Slice(0, stop);
            else
                return bb;
        }

        public override bool CanReset => Inner.CanReset;

        public override ValueTask ResetAsync()
        {
            return Inner.ResetAsync();
        }

        public override async ValueTask<Bucket> DuplicateAsync(bool reset)
        {
            return new EolNormalizeBucket(
                    await Inner.DuplicateAsync(reset).ConfigureAwait(false),
                    _acceptedEols,
                    _producedEol);
        }
    }
}
