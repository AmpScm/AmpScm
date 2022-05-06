using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    public sealed class GitObjectFileBucket : GitObjectBucket, IBucketPoll
    {
        long _startOffset;
        long? _length;
        GitObjectType _type;

        public GitObjectFileBucket(Bucket inner)
            : base(new ZLibBucket(inner, BucketCompressionAlgorithm.ZLib))
        {
        }

        public override async ValueTask<GitObjectType> ReadTypeAsync()
        {
            int so = 0;
            while (_type == GitObjectType.None && _startOffset < 2)
            {
                var bb = await Inner.ReadAsync(2 - (int)_startOffset).ConfigureAwait(false);

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
                    throw new BucketEofException(Inner);
            }

            return _type;
        }

        public override long? Position
        {
            get
            {
                if (!_length.HasValue)
                    return 0;

                var p = Inner.Position;

                if (p < _startOffset)
                    return 0;
                else
                    return p - _startOffset;
            }
        }

        const int MaxReadForHeader = 5 /* "commit" */+ 1 /* " " */ + 20 /* UInt64.MaxValue.ToString().Length */ + 1 /* '\1' */;

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (_type == GitObjectType.None)
            {
                await ReadTypeAsync().ConfigureAwait(false);
            }

            if (!_length.HasValue)
            {
                var (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.Zero, requested: MaxReadForHeader).ConfigureAwait(false);

                if (eol != BucketEol.Zero)
                    throw new BucketException($"Expected '\\0' within first {MaxReadForHeader} characters of '{Inner.Name}'");


                int nSize = bb.IndexOf(' ');

                if (nSize > 0 && long.TryParse(bb.ToASCIIString(nSize + 1, bb.Length - nSize - 1, eol), out var len))
                    _length = len;
                else
                    throw new BucketException($"Expected length information within header of '{Inner.Name}'");

                _startOffset = Inner.Position!.Value;
            }

            return _length.Value - Position;
        }

        public override BucketBytes Peek()
        {
            if (_startOffset == 0)
                return BucketBytes.Empty;
            else
                return Inner.Peek();
        }

        ValueTask<BucketBytes> IBucketPoll.PollAsync(int minRequested /*= 1*/)
        {
            if (_startOffset == 0)
                return BucketBytes.Empty;

            return Inner.PollAsync(minRequested);
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (_length is null)
                await ReadRemainingBytesAsync().ConfigureAwait(false);

            return await Inner.ReadAsync(requested).ConfigureAwait(false);
        }

        public override bool CanReset => Inner.CanReset;

        public override async ValueTask ResetAsync()
        {
            await base.ResetAsync().ConfigureAwait(false);

            _startOffset = 0; // Handles skip and offset
            _length = 0;
        }
    }
}
