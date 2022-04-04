using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git.Buckets.Objects
{
    public sealed class GitCommitReadBucket : GitBucket
    {
        GitId? _treeId;
        IReadOnlyCollection<GitId>? _parents;
        GitSignatureRecord? _author;
        GitSignatureRecord? _committer;

        public GitCommitReadBucket(Bucket inner)
            : base(inner)
        {
        }

        const BucketEol AcceptedEols = BucketEol.LF | BucketEol.CRLF;
        const int MaxHeader = 1024;

        public async ValueTask<GitId> ReadTreeIdAsync()
        {
            if (_treeId is not null)
                return _treeId;

            var (bb, eol) = await Inner.ReadUntilEolFullAsync(AcceptedEols, null, 5 /* "tree " */ + GitId.MaxHashLength * 2 + 2 /* ALL EOL */).ConfigureAwait(false);

            if (bb.IsEof || eol == BucketEol.None || !bb.StartsWithASCII("tree "))
                throw new GitBucketException($"Expected 'tree' record at start of commit in '{Inner.Name}'");

            if (GitId.TryParse(bb.Slice(5, bb.Length - 5 - eol.CharCount()), out var id))
                _treeId = id;
            else
                throw new GitBucketException($"Expected valid 'tree' record at start of commit in '{Inner.Name}'");

            return _treeId;
        }

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

            var (bb, eol) = await Inner.ReadUntilEolFullAsync(AcceptedEols, requested: "parent ".Length + GitId.MaxHashLength * 2 + 2 /* ALL EOL */).ConfigureAwait(false);

            if (bb.IsEof || eol == BucketEol.None || !bb.StartsWithASCII("parent "))
            {
                if (bb.IsEof || !bb.StartsWithASCII("author "))
                    throw new GitBucketException($"Expected 'parent' or 'author', but got neither in commit '{Inner.Name}'");

                _parents = Array.Empty<GitId>();

                // We accidentally read the first part of the author line. Let's keep things clean

                var authorStart = bb.ToArray();

                (bb, eol) = await Inner.ReadUntilEolFullAsync(AcceptedEols, requested: MaxHeader - bb.Length).ConfigureAwait(false);

                bb = authorStart.Concat(bb.Slice(bb.Length - eol.CharCount()).ToArray()).ToArray();

                _author = GitSignatureRecord.TryReadFromBucket(bb.Slice("author ".Length), out var author) ? author : throw new GitBucketException($"Invalid author line in {Inner.Name}");
                return null;
            }
            else if (GitId.TryParse(bb.Slice(7, bb.Length), out var id))
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
            else
                parents = new();

            while (true)
            {
                var (bb, eol) = await Inner.ReadUntilEolFullAsync(AcceptedEols, requested: MaxHeader).ConfigureAwait(false);

                if (!bb.IsEof && bb.StartsWithASCII("parent "))
                {
                    if (GitId.TryParse(bb.Slice(7, bb.Length - "parent ".Length - eol.CharCount()), out var id))
                    {
                        parents.Add(id);
                        continue;
                    }
                    else
                        throw new GitBucketException($"Bad parent header in '{Inner.Name}'");
                }
                else if (!bb.IsEof && bb.StartsWithASCII("author "))
                {
                    _author = GitSignatureRecord.TryReadFromBucket(bb.Slice("author ".Length, eol), out var author) ? author : throw new GitBucketException($"Invalid author line in {Inner.Name}");
                    return _parents = parents.AsReadOnly();
                }
                else if (bb.IsEof)
                    return _parents = parents.Count > 0 ? parents.AsReadOnly() : Array.Empty<GitId>();
                else
                    throw new GitBucketException($"Expected 'parent' or 'author', but got neither in commit '{Inner.Name}'");
            }
        }

        public async ValueTask<GitSignatureRecord> ReadAuthorAsync()
        {
            if (_author is null)
                await ReadAllParentIdsAsync().ConfigureAwait(false);

            return _author ?? throw new GitBucketException($"Unable to read author header from '{Inner.Name}'");
        }

        public async ValueTask<GitSignatureRecord> ReadCommitter()
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

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            await ReadCommitter().ConfigureAwait(false);

            return await Inner.ReadAsync(requested).ConfigureAwait(false);
        }
    }
}
