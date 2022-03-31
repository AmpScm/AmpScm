using System;
using System.Linq;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Client.Http
{
    internal class HttpDechunkBucket : WrappingBucket
    {
        enum DechunkState
        {
            Start, // Before next size block
            Size, // Within size block
            Chunk, // Within a chunk
            Term, // Within the CRLF at the end of the cunk
            Fin, // Last CRLF before EOF
            Fin2, // Last LF  before EOF
            Eof // done
        }

        DechunkState _state;
        int _chunkLeft;
        byte[]? _start;
        BucketEol _eol;

        public HttpDechunkBucket(Bucket inner, bool noDispose)
            : base(inner, noDispose)
        {
        }

        public override BucketBytes Peek()
        {
            switch (_state)
            {
                case DechunkState.Chunk:
                    var bb = Inner.Peek();

                    if (bb.Length > _chunkLeft)
                        return bb.Slice(0, _chunkLeft);

                    return bb;
                case DechunkState.Eof:
                    return BucketBytes.Eof;
                default:
                    Advance(false).AsTask().GetAwaiter().GetResult(); // Never waits!

                    if (_state == DechunkState.Chunk)
                        goto case DechunkState.Chunk;
                    return BucketBytes.Empty;
            }
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            switch (_state)
            {
                case DechunkState.Chunk:
                    {
                        var bb = await Inner.ReadAsync(Math.Min(requested, _chunkLeft)).ConfigureAwait(false);

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
                    await Advance(true).ConfigureAwait(false);

                    if (_state == DechunkState.Chunk)
                        goto case DechunkState.Chunk;
                    else if (_state == DechunkState.Eof)
                        goto case DechunkState.Eof;
                    else
                        throw new InvalidOperationException();
            }
        }

        async ValueTask Advance(bool wait)
        {
            while (_state != DechunkState.Chunk && _state != DechunkState.Eof)
            {
                if (!wait)
                {
                    var bb = Inner.Peek();

                    if (bb.IsEmpty)
                        return;
                }

                switch (_state)
                {
                    case DechunkState.Start:
                        {
                            var (bb, eol) = await Inner.ReadUntilEolAsync(BucketEol.CRLF | BucketEol.LF).ConfigureAwait(false);

                            if (eol == BucketEol.CRLF || eol == BucketEol.LF)
                            {
                                _chunkLeft = Convert.ToInt32(bb.ToASCIIString(eol), 16);
                                _state = _chunkLeft > 0 ? DechunkState.Chunk : DechunkState.Fin;
                            }
                            else if (bb.IsEof)
                                throw new HttpBucketException("Unexpected EOF");
                            else
                            {
                                _state = DechunkState.Size;
                                _start = bb.ToArray();
                                _eol = eol;
                            }
                        }
                        break;
                    case DechunkState.Size:
                        {
                            var (bb, eol) = await Inner.ReadUntilEolAsync(BucketEol.CRLF | BucketEol.LF).ConfigureAwait(false);

                            if (eol != BucketEol.None && eol != BucketEol.CRSplit)
                            {
                                bb = _start!.Concat(bb.ToArray()).ToArray();
                                _chunkLeft = Convert.ToInt32(bb.ToASCIIString().Trim(), 16);
                                _state = _chunkLeft > 0 ? DechunkState.Chunk : DechunkState.Fin;
                            }
                            else
                            {
                                _start = _start!.Concat(bb.ToArray()).ToArray();
                                _eol = eol;
                            }
                        }
                        break;
                    case DechunkState.Term:
                        {
                            var bb = await Inner.ReadAsync(_chunkLeft).ConfigureAwait(false);
                            _chunkLeft -= bb.Length;

                            if (bb.IsEof)
                                throw new HttpBucketException("Unexpected EOF");

                            if (_chunkLeft == 0)
                                _state = DechunkState.Start;
                        }
                        break;
                    case DechunkState.Fin:
                        {
                            var bb = await Inner.ReadAsync(2).ConfigureAwait(false);
                            if (bb.Length == 2)
                                _state = DechunkState.Eof;
                            else if (bb.Length == 1)
                                _state = DechunkState.Fin2;
                            else
                                throw new HttpBucketException("Unexpected EOF");
                        }
                        break;
                    case DechunkState.Fin2:
                        {
                            var bb = await Inner.ReadAsync(1).ConfigureAwait(false);
                            if (bb.Length == 1)
                                _state = DechunkState.Eof;
                            else
                                throw new HttpBucketException("Unexpected EOF");
                        }
                        break;
                    case DechunkState.Chunk:
                    case DechunkState.Eof:
                        return;

                }
            }
        }
    }
}
