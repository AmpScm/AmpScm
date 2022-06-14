using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git.Objects
{
    public sealed class GitTagObjectBucket : GitBucket, IBucketPoll
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitId? _objectId;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitObjectType _type;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string? _tagName;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitSignatureRecord? _author;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool _readHeaders;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        byte[]? _signature;

        public GitTagObjectBucket(Bucket inner)
            : base(inner)
        {
        }

        const BucketEol AcceptedEols = BucketEol.LF;
        const int MaxHeader = 1024;

        public async ValueTask<(GitId, GitObjectType)> ReadObjectIdAsync()
        {
            if (_objectId is not null)
                return (_objectId, _type);

            var (bb, eol) = await Inner.ReadExactlyUntilEolAsync(AcceptedEols, null, 7 /* "object " */ + GitId.MaxHashLength * 2 + 2 /* ALL EOL */).ConfigureAwait(false);

            if (bb.IsEof || eol == BucketEol.None || !bb.StartsWithASCII("object "))
                throw new GitBucketException($"Expected 'object' record at start of tag in '{Inner.Name}'");

            if (GitId.TryParse(bb.Slice(7, eol), out var id))
                _objectId = id;
            else
                throw new GitBucketException($"Expected valid 'object' record at start of tag in '{Inner.Name}'");

            (bb, eol) = await Inner.ReadExactlyUntilEolAsync(AcceptedEols, null, 5 /* "type " */ + 6 /* "commit" */ + 2 /* ALL EOL */).ConfigureAwait(false);

            if (bb.IsEof || eol == BucketEol.None || !bb.StartsWithASCII("type "))
            {
                _objectId = null;
                throw new GitBucketException($"Expected 'type' record of tag in '{Inner.Name}'");
            }

            bb = bb.Slice(5, eol);

            if (bb.EqualsASCII("commit"))
                _type = GitObjectType.Commit;
            else if (bb.EqualsASCII("tree"))
                _type = GitObjectType.Tree;
            else if (bb.EqualsASCII("blob"))
                _type = GitObjectType.Blob;
            else if (bb.EqualsASCII("tag"))
                _type = GitObjectType.Tag;
            else
                throw new GitBucketException($"Expected valid 'type' record in tag in '{Inner.Name}'");

            return (_objectId, _type);
        }

        public async ValueTask<string> ReadTagNameAsync()
        {
            if (_tagName is not null)
                return _tagName;

            if (_objectId is null)
                await ReadObjectIdAsync().ConfigureAwait(false);

            var (bb, eol) = await Inner.ReadExactlyUntilEolAsync(AcceptedEols, null, MaxHeader).ConfigureAwait(false);

            if (bb.IsEof || eol == BucketEol.None || !bb.StartsWithASCII("tag "))
                throw new GitBucketException($"Expected 'tag' record in '{Inner.Name}'");

            return _tagName = bb.ToUTF8String("tag ".Length, eol);
        }

        public async ValueTask<GitSignatureRecord> ReadTaggerAsync()
        {
            if (_author is null)
            {
                if (_tagName is null)
                    await ReadTagNameAsync().ConfigureAwait(false);

                var (bb, eol) = await Inner.ReadExactlyUntilEolAsync(AcceptedEols, requested: MaxHeader).ConfigureAwait(false);

                if (bb.StartsWithASCII("tagger ")
                    && GitSignatureRecord.TryReadFromBucket(bb.Slice("tagger ".Length, eol), out var author))
                {
                    _author = author;
                }
                else if (bb.IsEmpty(eol))
                {
                    _author = new GitSignatureRecord() { When = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero) };
                    _readHeaders = true;
                    // Special case. Bad commit in linux repository doesn't have a tagger
                }
                else
                    throw new GitBucketException($"Expected 'tagger' in tag in '{Inner.Name}'");
            }

            return _author;
        }


        async ValueTask ReadOtherHeadersAsync()
        {
            if (_readHeaders)
                return;

            await ReadTaggerAsync().ConfigureAwait(false);

            if (_readHeaders)
                return; // See special case. Bad commit in linux repository. See ReadTaggerAsync()

            while (true)
            {
                var (bb, eol) = await Inner.ReadExactlyUntilEolAsync(BucketEol.LF, null).ConfigureAwait(false);

                if (bb.IsEof || bb.Length <= eol.CharCount())
                    break;

                bb = bb.Slice(eol);

                var parts = bb.SplitToUtf8String((byte)' ', 2);
                switch (parts[0])
                {
                    default:
                        break;
                }
            }

            _readHeaders = true;
        }

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (_readHeaders)
                return Inner.ReadRemainingBytesAsync();

            return base.ReadRemainingBytesAsync();
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            if (!_readHeaders)
            {
                await ReadOtherHeadersAsync().ConfigureAwait(false);
            }

            while (true)
            {
                var (bb, eol) = await Inner.ReadExactlyUntilEolAsync(BucketEol.LF, requested: requested).ConfigureAwait(false);

                if (OpenPgpArmorBucket.IsHeader(bb, eol))
                {
                    using var sig = new OpenPgpArmorBucket(bb.ToArray().AsBucket() + Inner.NoClose());

                    bb = await sig.ReadExactlyAsync(8192).ConfigureAwait(false);

                    _signature = bb.ToArray();

                    continue;
                }
                else
                    return bb;
            }
        }

        public override BucketBytes Peek()
        {
            if (_readHeaders)
                return Inner.Peek();

            return BucketBytes.Empty;
        }

        async ValueTask<BucketBytes> IBucketPoll.PollAsync(int minRequested/* = 1*/)
        {
            if (_readHeaders)
                return await Inner.PollAsync(minRequested).ConfigureAwait(false);

            return BucketBytes.Empty;
        }

        public ValueTask<BucketBytes> ReadSignatureBytesAsync()
        {
            return new(_signature ?? BucketBytes.Empty);
        }
    }
}
