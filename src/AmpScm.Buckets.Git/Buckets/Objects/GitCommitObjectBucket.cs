using System;
using System.Collections.Generic;
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
        GitId? _treeId;
        IReadOnlyCollection<GitId>? _parents;
        GitSignatureRecord? _author;
        GitSignatureRecord? _committer;
        bool _readHeaders;

        public GitCommitObjectBucket(Bucket inner)
            : base(inner)
        {
        }

        const BucketEol AcceptedEols = BucketEol.LF;
        const int MaxHeader = 1024;

        public async ValueTask<GitId> ReadTreeIdAsync()
        {
            if (_treeId is not null)
                return _treeId;

            var (bb, eol) = await Inner.ReadUntilEolFullAsync(AcceptedEols, null, 5 /* "tree " */ + GitId.MaxHashLength * 2 + 2 /* ALL EOL */).ConfigureAwait(false);

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
            var (bb, eol) = await Inner.ReadUntilEolFullAsync(AcceptedEols, requested: ParentLineReadLength).ConfigureAwait(false);

            if (bb.IsEof || eol == BucketEol.None || !bb.StartsWithASCII("parent "))
            {
                if (bb.IsEof || !bb.StartsWithASCII("author "))
                    throw new GitBucketException($"Expected 'parent' or 'author', but got neither in commit {Name} Bucket");

                _parents = Array.Empty<GitId>();

                // We accidentally read the first part of the author line. Let's keep things clean

                if (eol == BucketEol.None)
                {
                    var authorBucket = new AggregateBucket(bb.Slice("author ".Length).ToArray().AsBucket(), Inner).NoClose();
                    (bb, eol) = await authorBucket.ReadUntilEolFullAsync(AcceptedEols, requested: MaxHeader).ConfigureAwait(false);
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
                var (bb, eol) = await Inner.ReadUntilEolFullAsync(AcceptedEols, requested: MaxHeader).ConfigureAwait(false);

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
                        var authorBucket = new AggregateBucket(bb.Slice("author ".Length).ToArray().AsBucket(), Inner).NoClose();
                        (bb, eol) = await authorBucket.ReadUntilEolFullAsync(AcceptedEols, requested: MaxHeader).ConfigureAwait(false);
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
                    await ReadAllParentIdsAsync().ConfigureAwait(false);

                var (bb, eol) = await Inner.ReadUntilEolFullAsync(AcceptedEols, requested: MaxHeader).ConfigureAwait(false);

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

            var (bb, eol) = await Inner.ReadUntilEolFullAsync(AcceptedEols, requested: MaxHeader).ConfigureAwait(false);

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
                var (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.LF, null).ConfigureAwait(false);

                if (bb.IsEof || bb.Length <= eol.CharCount())
                    break;

                bb = bb.Slice(eol);

                var parts = bb.SplitToUtf8String((byte)' ', 2);
                switch (parts[0])
                {
                    case "mergetag":
                        break;

                    case "encoding":
                    case "gpgsig":
                        break; // Ignored for now

                    default:
                        //if (!char.IsWhiteSpace((char)bb[0]))
                        //{
                        //    _headers ??= new Dictionary<string, string>();
                        //    if (_headers.TryGetValue(parts[0], out var v))
                        //        _headers[parts[0]] = v + "\n" + parts[1];
                        //    else
                        //        _headers[parts[0]] = parts[1];
                        //}
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

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
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
