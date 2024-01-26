using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    public sealed class GitFileObjectBucket : GitObjectBucket, IBucketPoll
    {
        private long _startOffset;
        private long? _length;
        private GitObjectType _type;

        public GitFileObjectBucket(Bucket source)
            : base(new ZLibBucket(source, BucketCompressionAlgorithm.ZLib))
        {
        }

        private GitFileObjectBucket(ZLibBucket newInner)
            : base(newInner)
        {

        }

        public override Bucket Duplicate(bool reset = false)
        {
            var inner = Source.Duplicate(reset || Position == 0);

            GitFileObjectBucket dup = new GitFileObjectBucket((ZLibBucket)inner);
            dup._startOffset = _startOffset;
            dup._length = _length;
            dup._type = _type;
            return dup;
        }

        public override async ValueTask<GitObjectType> ReadTypeAsync()
        {
            int so = 0;
            while (_type == GitObjectType.None && _startOffset < 2)
            {
                var bb = await Source.ReadAsync(2 - (int)_startOffset).ConfigureAwait(false);

                so += bb.Length;

                if (so == 2)
                {
                    switch ((char)bb[bb.Length - 1])
                    {
                        case 'l':
                            _type = GitObjectType.Blob;
                            break;
                        case 'o':
                            _type = GitObjectType.Commit;
                            break;
                        case 'r':
                            _type = GitObjectType.Tree;
                            break;
                        case 'a':
                            _type = GitObjectType.Tag;
                            break;
                        default:
                            throw new GitBucketException($"Unexpected object type in '{Name}' bucket");
                    }
                    return _type; // We know enough
                }
                else if (bb.Length == 1)
                {
                    switch ((char)bb[0])
                    {
                        case 'b':
                            _type = GitObjectType.Blob;
                            break;
                        case 'c':
                            _type = GitObjectType.Commit;
                            break;
                        case 't':
                            continue; // Really need additional byte
                        default:
                            throw new GitBucketException($"Unexpected object type in '{Name}' bucket");
                    }
                    return _type;
                }
                else
                    throw new BucketEofException(Source);
            }

            return _type;
        }

        public override long? Position
        {
            get
            {
                if (!_length.HasValue)
                    return 0;

                var p = Source.Position;

                if (p < _startOffset)
                    return 0;
                else
                    return p - _startOffset;
            }
        }

        private const int MaxReadForHeader = 5 /* "commit" */+ 1 /* " " */ + 20 /* UInt64.MaxValue.ToString().Length */ + 1 /* '\1' */;

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (_type == GitObjectType.None)
            {
                await ReadTypeAsync().ConfigureAwait(false);
            }

            if (!_length.HasValue)
            {
                var (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.Zero, requested: MaxReadForHeader).ConfigureAwait(false);

                if (eol != BucketEol.Zero)
                    throw new BucketException($"Expected '\\0' within first {MaxReadForHeader} characters of '{Source.Name}'");

                int nSize = bb.IndexOf(' ');

                if (nSize > 0 && long.TryParse(bb.ToASCIIString(nSize + 1, eol), out var len))
                    _length = len;
                else
                    throw new BucketException($"Expected length information within header of '{Source.Name}'");

                _startOffset = Source.Position!.Value;
            }

            return _length.Value - Position;
        }

        public override BucketBytes Peek()
        {
            if (_startOffset == 0)
                return BucketBytes.Empty;
            else
                return Source.Peek();
        }

        ValueTask<BucketBytes> IBucketPoll.PollAsync(int minRequested /*= 1*/)
        {
            if (_startOffset == 0)
                return BucketBytes.Empty;

            return Source.PollAsync(minRequested);
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            if (_length is null)
                await ReadRemainingBytesAsync().ConfigureAwait(false);

            return await Source.ReadAsync(requested).ConfigureAwait(false);
        }

        public override bool CanReset => Source.CanReset;

        public override void Reset()
        {
            base.Reset();

            _startOffset = 0; // Handles skip and offset
            _length = 0;
        }

        public override async ValueTask SeekAsync(long newPosition)
        {
            if (_startOffset < 4)
            {
                await ReadRemainingBytesAsync().ConfigureAwait(false);
            }
            await Source.SeekAsync(newPosition + _startOffset).ConfigureAwait(false);
        }
    }
}
