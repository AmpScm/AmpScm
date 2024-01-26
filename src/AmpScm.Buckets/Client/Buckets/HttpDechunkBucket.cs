using System;
using System.Linq;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Client.Buckets
{
    internal sealed class HttpDechunkBucket : WrappingBucket
    {
        private enum DechunkState
        {
            Start, // Before next size block
            Size, // Within size block
            Chunk, // Within a chunk
            Term, // Within the CRLF at the end of the chunk
            Fin, // Last CRLF before EOF
            Fin2, // Last LF  before EOF
            Eof // done
        }

        private DechunkState _state;
        private int _chunkLeft;
        private byte[]? _start;
        private readonly bool _noFin;

        public HttpDechunkBucket(Bucket source, bool leaveFinalEol = false)
            : base(source)
        {
            _noFin = leaveFinalEol;
        }

        public override BucketBytes Peek()
        {
            switch (_state)
            {
                case DechunkState.Chunk:
                    var bb = Source.Peek();

                    if (bb.Length > _chunkLeft)
                        return bb.Slice(0, _chunkLeft);

                    return bb;
                case DechunkState.Eof:
                    return BucketBytes.Eof;
                default:
                    Advance(wait: false).AsTask().Wait(); // Never waits!

                    if (_state == DechunkState.Chunk)
                        goto case DechunkState.Chunk;
                    return BucketBytes.Empty;
            }
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            switch (_state)
            {
                case DechunkState.Chunk:
                    {
                        var bb = await Source.ReadAsync(Math.Min(requested, _chunkLeft)).ConfigureAwait(false);

                        _chunkLeft -= bb.Length;
                        if (_chunkLeft == 0)
                        {
                            _state = DechunkState.Term;
                            _chunkLeft = 2; // CRLF
                        }
                        return bb;
                    }
                case DechunkState.Eof:
                    return BucketBytes.Eof;
                default:
                    await Advance(wait: true).ConfigureAwait(false);

                    if (_state == DechunkState.Chunk)
                        goto case DechunkState.Chunk;
                    else if (_state == DechunkState.Eof)
                        goto case DechunkState.Eof;
                    else
                        throw new InvalidOperationException();
            }
        }

        private async ValueTask Advance(bool wait)
        {
            while (_state != DechunkState.Chunk && _state != DechunkState.Eof)
            {
                if (!wait)
                {
                    var bb = Source.Peek();

                    if (bb.IsEmpty)
                        return;
                }

                switch (_state)
                {
                    case DechunkState.Start:
                        {
                            var (bb, eol) = await Source.ReadUntilEolAsync(BucketEol.CRLF | BucketEol.LF).ConfigureAwait(false);

                            if (eol == BucketEol.CRLF || eol == BucketEol.LF)
                            {
                                if (bb.Length > eol.CharCount())
                                    _chunkLeft = Convert.ToInt32(bb.ToASCIIString(eol), 16);
                                else
                                    _chunkLeft = 0;
                                _state = _chunkLeft > 0 ? DechunkState.Chunk : (_noFin ? DechunkState.Eof : DechunkState.Fin);
                            }
                            else if (bb.IsEof)
                                throw new HttpBucketException($"Unexpected EOF in {Name} Bucket");
                            else
                            {
                                _state = DechunkState.Size;
                                _start = bb.ToArray();

                                if (_start.Length > 16 || _start.Any(x => !IsHexCharOrCR(x)))
                                    throw new HttpBucketException($"Invalid chunk header in {Name} Bucket");
                            }
                        }
                        break;
                    case DechunkState.Size:
                        {
                            var (bb, eol) = await Source.ReadUntilEolAsync(BucketEol.CRLF | BucketEol.LF).ConfigureAwait(false);

                            if (eol != BucketEol.None && eol != BucketEol.CRSplit)
                            {
                                bb = _start!.AppendBytes(bb);
                                // Strip final '\r' if any, before passing to ToInt32
                                _chunkLeft = Convert.ToInt32(bb.Trim(eol).ToASCIIString(), 16);
                                _state = _chunkLeft > 0 ? DechunkState.Chunk : (_noFin ? DechunkState.Eof : DechunkState.Fin);
                            }
                            else if (bb.IsEof)
                                throw new HttpBucketException($"Unexpected EOF in {Name} Bucket");
                            else
                            {
                                _start = _start!.AppendBytes(bb);

                                if (_start.Length > 16 || _start.Any(x => !IsHexCharOrCR(x)))
                                    throw new HttpBucketException($"Invalid chunk header in {Name} Bucket");
                            }
                        }
                        break;
                    case DechunkState.Term:
                        {
                            var bb = await Source.ReadAsync(_chunkLeft).ConfigureAwait(false);
                            _chunkLeft -= bb.Length;

                            if (bb.IsEof)
                                throw new HttpBucketException($"Unexpected EOF in {Name} Bucket");

                            if (_chunkLeft == 0)
                                _state = DechunkState.Start;
                        }
                        break;
                    case DechunkState.Fin:
                        {
                            var bb = await Source.ReadAsync(2).ConfigureAwait(false);
                            if (bb.Length == 2)
                                _state = DechunkState.Eof;
                            else if (bb.Length == 1)
                                _state = DechunkState.Fin2;
                            else
                                throw new HttpBucketException($"Unexpected EOF in {Name} Bucket");
                        }
                        break;
                    case DechunkState.Fin2:
                        {
                            var bb = await Source.ReadAsync(1).ConfigureAwait(false);
                            if (bb.Length == 1)
                                _state = DechunkState.Eof;
                            else
                                throw new HttpBucketException($"Unexpected EOF in {Name} Bucket");
                        }
                        break;
                    case DechunkState.Chunk:
                    case DechunkState.Eof:
                        return;

                }
            }
        }

        private static bool IsHexCharOrCR(byte x)
        {
            if (x >= '0' && x <= '9')
                return true;
            else if (x >= 'A' && x <= 'F')
                return true;
            else if (x >= 'a' && x <= 'f')
                return true;
            else if (x == '\r')
                return true;
            else
                return false;
        }
    }
}
