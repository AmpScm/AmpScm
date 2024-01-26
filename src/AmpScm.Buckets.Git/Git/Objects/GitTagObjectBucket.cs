using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Cryptography;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git.Objects;

public sealed class GitTagObjectBucket : GitBucket, IBucketPoll
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private GitId? _objectId;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private GitObjectType _type;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string? _tagName;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private GitSignatureRecord? _author;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _readHeaders;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private byte[]? _signature;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly Func<GitSubBucketType, Bucket, ValueTask>? _handleSubBucket;

    public GitTagObjectBucket(Bucket source)
        : this(source, handleSubBucket: null)
    {
    }

    public GitTagObjectBucket(Bucket source, Func<GitSubBucketType, Bucket, ValueTask>? handleSubBucket)
        : base(source)
    {
        _handleSubBucket = handleSubBucket;
    }

    private const BucketEol AcceptedEols = BucketEol.LF;
    private const int MaxHeader = 1024;

    public async ValueTask<(GitId, GitObjectType)> ReadObjectIdAsync()
    {
        if (_objectId is not null)
            return (_objectId, _type);

        var (bb, eol) = await Source.ReadExactlyUntilEolAsync(AcceptedEols, 7 /* "object " */ + GitId.MaxHashLength * 2 + 2 /* ALL EOL */, eolState: null).ConfigureAwait(false);

        if (bb.IsEof || eol == BucketEol.None || !bb.StartsWithASCII("object "))
            throw new GitBucketException($"Expected 'object' record at start of tag in '{Source.Name}'");

        if (GitId.TryParse(bb.Slice(7, eol), out var id))
            _objectId = id;
        else
            throw new GitBucketException($"Expected valid 'object' record at start of tag in '{Source.Name}'");

        (bb, eol) = await Source.ReadExactlyUntilEolAsync(AcceptedEols, 5 /* "type " */ + 6 /* "commit" */ + 2 /* ALL EOL */, eolState: null).ConfigureAwait(false);

        if (bb.IsEof || eol == BucketEol.None || !bb.StartsWithASCII("type "))
        {
            _objectId = null;
            throw new GitBucketException($"Expected 'type' record of tag in '{Source.Name}'");
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
            throw new GitBucketException($"Expected valid 'type' record in tag in '{Source.Name}'");

        return (_objectId, _type);
    }

    public async ValueTask<string> ReadTagNameAsync()
    {
        if (_tagName is not null)
            return _tagName;

        if (_objectId is null)
            await ReadObjectIdAsync().ConfigureAwait(false);

        var (bb, eol) = await Source.ReadExactlyUntilEolAsync(AcceptedEols, MaxHeader, eolState: null).ConfigureAwait(false);

        if (bb.IsEof || eol == BucketEol.None || !bb.StartsWithASCII("tag "))
            throw new GitBucketException($"Expected 'tag' record in '{Source.Name}'");

        return _tagName = bb.ToUTF8String("tag ".Length, eol);
    }

    public async ValueTask<GitSignatureRecord> ReadTaggerAsync()
    {
        if (_author is null)
        {
            if (_tagName is null)
                await ReadTagNameAsync().ConfigureAwait(false);

            var (bb, eol) = await Source.ReadExactlyUntilEolAsync(AcceptedEols, requested: MaxHeader).ConfigureAwait(false);

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
                throw new GitBucketException($"Expected 'tagger' in tag in '{Source.Name}'");
        }

        return _author;
    }

    private async ValueTask ReadOtherHeadersAsync()
    {
        if (_readHeaders)
            return;

        await ReadTaggerAsync().ConfigureAwait(false);

        if (_readHeaders)
            return; // See special case. Bad commit in linux repository. See ReadTaggerAsync()

        while (true)
        {
            var (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.LF, eolState: null).ConfigureAwait(false);

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
            return Source.ReadRemainingBytesAsync();

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
            var (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.LF, requested: requested).ConfigureAwait(false);

            if (Radix64ArmorBucket.IsHeader(bb, eol))
            {
                var src = bb.Memory.AsBucket() + Source.NoDispose();
                if (_handleSubBucket != null)
                {
                    await _handleSubBucket(GitSubBucketType.Signature, src).ConfigureAwait(false);
                }
                else
                {
                    using var sig = new Radix64ArmorBucket(src);

                    bb = await sig.ReadExactlyAsync(8192).ConfigureAwait(false);

                    _signature = bb.ToArray();
                }
                continue;
            }
            else
                return bb;
        }
    }

    public override BucketBytes Peek()
    {
        if (_readHeaders)
            return Source.Peek();

        return BucketBytes.Empty;
    }

    async ValueTask<BucketBytes> IBucketPoll.PollAsync(int minRequested/* = 1*/)
    {
        if (_readHeaders)
            return await Source.PollAsync(minRequested).ConfigureAwait(false);

        return BucketBytes.Empty;
    }

    public ValueTask<BucketBytes> ReadSignatureBytesAsync()
    {
        return new(_signature ?? BucketBytes.Empty);
    }

    public static Bucket ForSignature(Bucket src)
    {
        return Create.From(WalkSignature(src));
    }

    private static async IAsyncEnumerable<BucketBytes> WalkSignature(Bucket src)
    {
        using (src)
        {
            while (true)
            {
                var (bb, _) = await src.ReadExactlyUntilEolAsync(BucketEol.LF).ConfigureAwait(false);

                if (bb.IsEof)
                    yield break;

                if (bb.StartsWithASCII("-----BEGIN "))
                {
                    yield break;
                }

                yield return bb;
            }
        }
    }
}
