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
    public sealed class GitCommitObjectBucket : GitBucket, IBucketPoll
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitId? _treeId;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        IReadOnlyCollection<GitId>? _parents;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitSignatureRecord? _author;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitSignatureRecord? _committer;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool _readHeaders;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly Func<GitSubBucketType, Bucket, ValueTask>? _handleSubBucket;

        public GitCommitObjectBucket(Bucket inner)
            : this(inner, null)
        {
        }

        public GitCommitObjectBucket(Bucket inner, Func<GitSubBucketType, Bucket, ValueTask>? handleSubBucket)
            : base(inner)
        {
            _handleSubBucket = handleSubBucket;
        }

        const BucketEol AcceptedEols = BucketEol.LF;
        const int MaxHeader = 1024;

        public async ValueTask<GitId> ReadTreeIdAsync()
        {
            if (_treeId is not null)
                return _treeId;

            var (bb, eol) = await Inner.ReadExactlyUntilEolAsync(AcceptedEols, 5 /* "tree " */ + GitId.MaxHashLength * 2 + 2 /* ALL EOL */, null).ConfigureAwait(false);

            if (bb.IsEof || eol == BucketEol.None || !bb.StartsWithASCII("tree "))
                throw new GitBucketException($"Expected 'tree' record at start of commit in '{Inner.Name}'");

            if (GitId.TryParse(bb.Slice(5, eol), out var id))
                _treeId = id;
            else
                throw new GitBucketException($"Expected valid 'tree' record at start of commit in '{Inner.Name}'");

            return _treeId;
        }

        static readonly int ParentLineReadLength = "parent ".Length + GitId.MaxHashLength * 2 + 2 /* ALL EOL */;

        public async ValueTask<GitId?> ReadFirstParentIdAsync()
        {
            if (_parents is not null)
            {
                return _parents.First();
            }
            else if (_treeId is null)
            {
                await ReadTreeIdAsync().ConfigureAwait(false);
            }

            // Typically every commit has a parent, so optimize for that case
            var (bb, eol) = await Inner.ReadExactlyUntilEolAsync(AcceptedEols, requested: ParentLineReadLength).ConfigureAwait(false);

            if (bb.IsEof || eol == BucketEol.None || !bb.StartsWithASCII("parent "))
            {
                if (bb.IsEof || !bb.StartsWithASCII("author "))
                    throw new GitBucketException($"Expected 'parent' or 'author', but got neither in commit {Name} Bucket");

                _parents = Array.Empty<GitId>();

                // We accidentally read the first part of the author line. Let's keep things clean

                if (eol == BucketEol.None)
                {
                    var authorBucket = (bb.Slice("author ".Length).ToArray().AsBucket() + Inner);
                    (bb, eol) = await authorBucket.ReadExactlyUntilEolAsync(AcceptedEols, requested: MaxHeader).ConfigureAwait(false);
                }

                _author = GitSignatureRecord.TryReadFromBucket(bb.Slice(eol), out var author) ? author : throw new GitBucketException($"Invalid author line in {Name} Bucket");
                return null;
            }
            else if (GitId.TryParse(bb.Slice("parent ".Length, eol), out var id))
            {
                _parents = new List<GitId>() { id };
                return id;
            }
            else
                throw new GitBucketException($"Invalid parent line in '{Inner.Name}");
        }

        public async ValueTask<IReadOnlyCollection<GitId>> ReadAllParentIdsAsync()
        {
            List<GitId> parents;
            if (_parents is not null)
            {
                if (_parents is not List<GitId> lst)
                    return _parents; // Already done

                parents = lst; // Still collecting after ReadFirstParentIdAsync()
            }
            else if (_treeId is null)
            {
                await ReadTreeIdAsync().ConfigureAwait(false);
                parents = new();
            }
            else
                parents = new();

            while (true)
            {
                var (bb, eol) = await Inner.ReadExactlyUntilEolAsync(AcceptedEols, requested: MaxHeader).ConfigureAwait(false);

                if (bb.IsEof)
                    return _parents = parents.Count > 0 ? parents.AsReadOnly() : Array.Empty<GitId>();
                else if (bb.StartsWithASCII("parent ")
                    && GitId.TryParse(bb.Slice("parent ".Length, eol), out var id))
                {
                    parents.Add(id);

                    // Stop scanning if we don't have more parents
                    bb = Inner.Peek();
                    if (!bb.IsEmpty && bb[0] != 'p')
                    {
                        return _parents = parents.AsReadOnly();
                    }

                    continue;
                }
                else if (bb.StartsWithASCII("author "))
                {
                    // Auch. We overread.
                    if (eol == BucketEol.None)
                    {
                        var authorBucket = (bb.Slice("author ".Length).ToArray().AsBucket() + Inner);
                        (bb, eol) = await authorBucket.ReadExactlyUntilEolAsync(AcceptedEols, requested: MaxHeader).ConfigureAwait(false);
                    }

                    _author = GitSignatureRecord.TryReadFromBucket(bb.Slice(eol), out var author) ? author : throw new GitBucketException($"Invalid author line in {Name} Bucket");
                    return _parents = parents.AsReadOnly();
                }
                else
                    throw new GitBucketException($"Expected 'parent' or 'author', but got neither in commit '{Inner.Name}'");
            }
        }

        public async ValueTask<GitSignatureRecord> ReadAuthorAsync()
        {
            if (_author is null)
            {
                if (_parents is null || _parents is List<GitId>)
                {
                    await ReadAllParentIdsAsync().ConfigureAwait(false);

                    if (_author is not null)
                        return _author;
                }

                var (bb, eol) = await Inner.ReadExactlyUntilEolAsync(AcceptedEols, requested: MaxHeader).ConfigureAwait(false);

                if (bb.StartsWithASCII("author ")
                    && GitSignatureRecord.TryReadFromBucket(bb.Slice("author ".Length, eol), out var author))
                {
                    _author = author;
                }
                else
                    throw new GitBucketException($"Expected 'author' in commit '{Inner.Name}'");
            }

            return _author ?? throw new GitBucketException($"Unable to read author header from '{Inner.Name}'");
        }

        public async ValueTask<GitSignatureRecord> ReadCommitterAsync()
        {
            if (_committer is not null)
                return _committer;
            else if (_author is null)
                await ReadAuthorAsync().ConfigureAwait(false);

            var (bb, eol) = await Inner.ReadExactlyUntilEolAsync(AcceptedEols, requested: MaxHeader).ConfigureAwait(false);

            if (bb.IsEof
                || eol == BucketEol.None
                || !bb.StartsWithASCII("committer ")
                || !GitSignatureRecord.TryReadFromBucket(bb.Slice("committer ".Length, eol), out var cm))
            {
                throw new GitBucketException($"Unable to read committer header from '{Inner.Name}'");
            }

            return _committer = cm;
        }

        async ValueTask ReadOtherHeadersAsync()
        {
            if (_readHeaders)
                return;

            await ReadCommitterAsync().ConfigureAwait(false);

            while (true)
            {
                var (bb, eol) = await Inner.ReadExactlyUntilEolAsync(BucketEol.LF, eolState: null).ConfigureAwait(false);

                if (bb.IsEof || bb.Length <= eol.CharCount())
                    break;

                // parts[1] contains the EOL!

                var parts = bb.SplitToUtf8String((byte)' ', 2);
                Bucket sub;
                switch (parts[0])
                {
                    case "mergetag":
                    case "gpgsig":
                    case "gpgsig-sha256":
                        sub = new GitLineUnindentBucket(bb.Slice(parts[0].Length).ToArray().AsBucket() + Inner.NoDispose());

                        if (_handleSubBucket is not null)
                            await _handleSubBucket(GetEvent(parts[0]), sub).ConfigureAwait(false);
                        else
                            await sub.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
                        break;
                    case "encoding":
                        break; // Ignored for now

                    default:
                        break;
                }
            }

            _readHeaders = true;
        }

        public static Bucket ForSignature(Bucket src)
        {
            return Create.From(WalkSignature(src));
        }

        static async IAsyncEnumerable<BucketBytes> WalkSignature(Bucket src)
        {
            using (src)
            {
                while (true)
                {
                    var (bb, _) = await src.ReadExactlyUntilEolAsync(BucketEol.LF).ConfigureAwait(false);

                    if (bb.IsEof)
                        yield break;

                    if (bb.StartsWithASCII("gpgsig ") || bb.StartsWithASCII("gpgsig-sha256 "))
                    {
                        while (true)
                        {
                            (bb, _) = await src.ReadExactlyUntilEolAsync(BucketEol.LF).ConfigureAwait(false);

                            if (bb.IsEof)
                                yield break;
                            else if (bb.Length == 0 || bb[0] != ' ')
                                break;
                        }
                    }

                    if (!bb.IsEmpty)
                    {
                        yield return bb;
                    }
                    else if (bb.IsEof)
                        yield break;
                    else
                        break;
                }

                while (true)
                {
                    var bb = await src.ReadAsync().ConfigureAwait(false);

                    if (bb.IsEof)
                        yield break;

                    yield return bb;
                }
            }
        }

        static GitSubBucketType GetEvent(string key) =>
            key switch
            {
                "mergetag" => GitSubBucketType.MergeTag,
                "gpgsig" => GitSubBucketType.Signature,
                "gpgsig-sha256" => GitSubBucketType.SignatureSha256,
                _ => throw new NotImplementedException()
            };

        public override ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (_readHeaders)
                return Inner.ReadRemainingBytesAsync();

            return base.ReadRemainingBytesAsync();
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            await ReadOtherHeadersAsync().ConfigureAwait(false);

            return await Inner.ReadAsync(requested).ConfigureAwait(false);
        }

        public override BucketBytes Peek()
        {
            if (_readHeaders)
                return Inner.Peek();

            return BucketBytes.Empty;
        }

        async ValueTask<BucketBytes> IBucketPoll.PollAsync(int minRequested/* = 1*/)
        {
            if (_committer is not null)
                return await Inner.PollAsync(minRequested).ConfigureAwait(false);

            return BucketBytes.Empty;
        }
    }
}
