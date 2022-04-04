using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    public sealed class GitObjectFileBucket : GitObjectBucket
    {
        long _startOffset;
        long? _length;

        public GitObjectFileBucket(Bucket inner) 
            : base(new ZLibBucket(inner))
        {
        }

        public override async ValueTask ReadTypeAsync()
        {
            int so = 0;
            while (Type == GitObjectType.None && _startOffset < 2)
            {
                var bb = await Inner.ReadAsync(2 - (int)_startOffset).ConfigureAwait(false);

                so += bb.Length;

                if (so == 2)
                {
                    switch ((char)bb[bb.Length - 1])
                    {
                        case 'l':
                            Type = GitObjectType.Blob;
                            break;
                        case 'o':
                            Type = GitObjectType.Commit;
                            break;
                        case 'r':
                            Type = GitObjectType.Tree;
                            break;
                        case 'a':
                            Type = GitObjectType.Tag;
                            break;
                        default:
                            throw new GitBucketException($"Unexpected object type in '{Name}' bucket");
                    }
                    return; // We know enough
                }
                else if (bb.Length == 1)
                {
                    switch ((char)bb[0])
                    {
                        case 'b':
                            Type = GitObjectType.Blob;
                            break;
                        case 'c':
                            Type = GitObjectType.Commit;
                            break;
                        case 't':
                            continue; // Really need additional byte
                    }
                    return;
                }
                else
                    throw new GitBucketException($"Unexpected EOF in header of '{Name}' bucket");
            }
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

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (Type == GitObjectType.None)
            {
                await ReadTypeAsync().ConfigureAwait(false);
            }

            if (!_length.HasValue)
            {
                var (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.Zero, requested: 48).ConfigureAwait(false);

                if (eol != BucketEol.Zero)
                    throw new BucketException($"Expected '\\0' within first 50 characters of '{Inner.Name}'");


                int nSize = bb.IndexOf((byte)' ');

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
