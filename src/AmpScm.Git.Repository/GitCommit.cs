using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Git.Objects;
using AmpScm.Buckets.Cryptography;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Objects;
using AmpScm.Git.Sets;

namespace AmpScm.Git
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public sealed class GitCommit : GitObject, IGitLazy<GitCommit>
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitCommitObjectBucket? _rb;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        object? _tree;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        object[]? _parent;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string? _message;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string? _summary;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitSignature? _author;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitSignature? _committer;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitTagObject[]? _mergeTags;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool _hasSignature;

        internal GitCommit(GitRepository repository, GitObjectBucket objectReader, GitId id)
            : base(repository, id)
        {
            _rb = new GitCommitObjectBucket(objectReader, HandleCommitBucket);
        }

        private async ValueTask HandleCommitBucket(GitSubBucketType type, Bucket bucket)
        {
            switch (type)
            {
                case GitSubBucketType.MergeTag:
                    bucket = bucket.Buffer();
                    long len = (await bucket.ReadRemainingBytesAsync().ConfigureAwait(false)).Value;
                    GitId? id = GitId.Zero(Id.Type);

                    await (GitObjectType.Tag.CreateHeader(len) + bucket.NoDispose())
                        .GitHash(Repository.InternalConfig.IdType, v => id = v)
                        .ReadUntilEofAndCloseAsync().ConfigureAwait(false);
                    bucket.Reset();

                    GitTagObject tagOb = new GitTagObject(Repository, bucket, id);

                    await tagOb.ReadAsync().ConfigureAwait(false);

                    if (_mergeTags is null)
                        _mergeTags = new[] { tagOb };
                    else
                        _mergeTags.ArrayAppend(tagOb);

                    break;
                case GitSubBucketType.Signature:
                case GitSubBucketType.SignatureSha256:
#if DEBUG
                    using (var sig = new Radix64ArmorBucket(bucket))
                        await sig.ReadUntilEofAsync().ConfigureAwait(false);
#else
                    await bucket.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
#endif
                    _hasSignature = true;
                    break;
                default:
                    await bucket.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
                    break;
            }
        }

        public sealed override GitObjectType Type => GitObjectType.Commit;

        public GitId TreeId
        {
            get
            {
                if (_tree is GitId id)
                    return id;
                else if (_tree is GitTree tree)
                    return tree.Id;
                else
                {
                    id = _rb!.ReadTreeIdAsync().AsTask().Result;
                    _tree = id;
                    return id;
                }
            }
        }

        public GitTree Tree
        {
            get
            {
                if (_tree is GitTree tree)
                    return tree;
                else if (TreeId is GitId id)
                {
                    var t = Repository.ObjectRepository.GetByIdAsync<GitTree>(id).AsTask().Result; // BAD async

                    if (t != null)
                    {
                        _tree = t;
                        return t;
                    }
                }
                return null!;
            }
        }

        public GitCommit? Parent => GetParent(0, false);

        public GitId? ParentId => GetParentId(0, false);

        public int ParentCount
        {
            get
            {
                Read(false);

                return _parent!.Length;
            }
        }

        private GitCommit? GetParent(int index, bool viaIndex = true)
        {
            Read(false);

            if (index < 0 || index >= (_parent?.Length ?? 0))
            {
                if (index == 0 && !viaIndex)
                    return null;

                throw new ArgumentOutOfRangeException(nameof(index));
            }

            object p = _parent![index];

            if (p is GitCommit c)
                return c;

            var id = (GitId)p;

            c = Repository.ObjectRepository.GetByIdAsync<GitCommit>(id).AsTask().Result!;
            if (c != null)
                _parent[index] = c;
            return c;
        }
        private GitId? GetParentId(int index, bool viaIndex = true)
        {
            Read(false);

            if (index < 0 || index >= (_parent?.Length ?? 0))
            {
                if (index == 0 && !viaIndex)
                    return null;

                throw new ArgumentOutOfRangeException(nameof(index));
            }

            object p = _parent![index];

            if (p is GitCommit c)
                return c.Id;
            else
                return (GitId)p;
        }

        public IReadOnlyList<GitId> ParentIds => new IdList(this);

        public IReadOnlyList<GitCommit> Parents => new ParentList(this);

        public IReadOnlyList<GitTagObject?> MergeTags => new MergeTagList(this);

        /// <summary>
        /// Gets a boolean indicating whether the signature has at least one GPG signature
        /// </summary>
        public bool IsSigned
        {
            get
            {
                if (_rb is not null)
                    Read(true);

                return _hasSignature;
            }
        }


        public string Message
        {
            get
            {
                if (_message is null)
                    Read(true);

                return _message!;
            }
        }

        public string Summary
        {
            get
            {
                return _summary ??= GitTools.CreateSummary(Message);
            }
        }

        public GitSignature Author
        {
            get
            {
                if (_author is null)
                    Read(false);

                return _author!;
            }
        }

        public GitSignature Committer
        {
            get
            {
                if (_committer is null)
                    Read(false);

                return _committer!;
            }
        }

        private void Read(bool all)
        {
            if (!all && _committer is not null)
                return;
            ReadAsync(all).AsTask().Wait();
        }

        public override ValueTask ReadAsync()
        {
            return ReadAsync(true);
        }

        async ValueTask ReadAsync(bool all)
        {
            if (_rb is null)
                return;

            _tree ??= await _rb.ReadTreeIdAsync().ConfigureAwait(false);
            if (_parent is null)
            {
                var p = await _rb.ReadAllParentIdsAsync().ConfigureAwait(false);
                _parent = p.ToArray<object>();
            }

            _author ??= new GitSignature(await _rb.ReadAuthorAsync().ConfigureAwait(false));

            _committer ??= new GitSignature(await _rb.ReadCommitterAsync().ConfigureAwait(false));

            while (true)
            {
                var (bb, _) = await _rb.ReadExactlyUntilEolAsync(BucketEol.LF).ConfigureAwait(false);

                if (bb.IsEof)
                    break;

                _message += bb.ToUTF8String(); // Includes EOL
            }

            _rb.Dispose();
            _rb = null;
        }

        ValueTask<GitId> IGitLazy<GitCommit>.WriteToAsync(GitRepository repository)
        {
            if (repository != Repository && !repository.Commits.ContainsId(Id))
                return this.AsWriter().WriteToAsync(repository);
            else
                return new(Id);
        }

        public async ValueTask<bool> VerifySignatureAsync(Func<ReadOnlyMemory<byte>, ValueTask<GitPublicKey?>>? findKey = null, bool includeMergetags = true)
        {
            bool allSucceeded = true;
            bool foundSignature = false;
            bool disposeSrc = true;
            GitObjectBucket b = (await Repository.ObjectRepository.FetchGitIdBucketAsync(Id).ConfigureAwait(false))!;
            var src = GitCommitObjectBucket.ForSignature(b);

            using (var gob = new GitCommitObjectBucket(b.Duplicate(), HandleSubBucket))
            {
                await gob.ReadUntilEofAsync().ConfigureAwait(false);
            }
            if (!disposeSrc)
                src.Dispose();

            return allSucceeded && foundSignature;

            async ValueTask HandleSubBucket(GitSubBucketType sbType, Bucket commitBucket)
            {
                if (allSucceeded && sbType == GitSubBucketType.Signature || sbType == GitSubBucketType.SignatureSha256)
                {
                    var rdx = new Radix64ArmorBucket(commitBucket);
                    using var gpg = new SignatureBucket(rdx);

                    var fingerprint = await gpg.ReadFingerprintAsync().ConfigureAwait(false);

                    GitPublicKey? key = null;

                    if (findKey != null)
                        key = await findKey(fingerprint).ConfigureAwait(false);

                    if (key is null)
                        key = await Repository.PublicKeyRepository.GetKeyAsync(fingerprint).ConfigureAwait(false);

                    disposeSrc = false;

                    foundSignature = true;
                    if (key is not null)
                    {
                        if (!await gpg.VerifyAsync(src, key, unknownSignerOk: true).ConfigureAwait(false))
                            allSucceeded = false;
                    }
                    else
                    {
                        await src.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
                        allSucceeded = false;
                    }
                }
                else if (allSucceeded && sbType == GitSubBucketType.MergeTag && includeMergetags)
                {
                    commitBucket = commitBucket.Buffer();

                    GitTagObjectBucket tob = new GitTagObjectBucket(commitBucket, HandleTagBucket);

                    await tob.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
                }
                else
                    await commitBucket.ReadUntilEofAndCloseAsync().ConfigureAwait(false);

                async ValueTask HandleTagBucket(GitSubBucketType sbType, Bucket tagBucket)
                {
                    if (allSucceeded && sbType == GitSubBucketType.Signature)
                    {
                        var rdx = new Radix64ArmorBucket(tagBucket);
                        using var gpg = new SignatureBucket(rdx);

                        var fingerprint = await gpg.ReadFingerprintAsync().ConfigureAwait(false);

                        GitPublicKey? key = null;

                        if (findKey != null)
                            key = await findKey(fingerprint).ConfigureAwait(false);

                        if (key is null)
                            key = await Repository.PublicKeyRepository.GetKeyAsync(fingerprint).ConfigureAwait(false);

                        foundSignature = true;

                        commitBucket.Reset();

                        if (key is not null)
                        {
                            if (!await gpg.VerifyAsync(GitTagObjectBucket.ForSignature(commitBucket.NoDispose()), key, unknownSignerOk: true).ConfigureAwait(false))
                                allSucceeded = false;
                        }
                        else
                        {
                            await src.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
                            allSucceeded = false;
                        }
                    }
                    else
                        await tagBucket.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
                }
            }
        }

        public GitRevisionSet Revisions => new GitRevisionSet(Repository).AddCommit(this);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                if (_message == null)
                    return $"Commit {Id:x12}";
                else
                    return $"Commit {Id:x12} - {Summary}";
            }
        }

        [DebuggerDisplay($"Count={{{nameof(Count)}}}")]
        private sealed class IdList : IReadOnlyList<GitId>
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            GitCommit Commit { get; }

            public IdList(GitCommit commit)
            {
                Commit = commit;
                Commit.Read(false);
            }

            public GitId this[int index] => Commit.GetParentId(index)!;

            public int Count => Commit.ParentCount;

            public IEnumerator<GitId> GetEnumerator()
            {
                if (Commit._parent is object[] parents)
                {
                    for (int i = 0; i < parents.Length; i++)
                    {
                        if (parents[i] is GitId id)
                            yield return id;
                        else if (parents[i] is GitObject ob)
                            yield return ob.Id;
                        else
                            yield return Commit.GetParentId(i)!;
                    }
                }
                else
                {
                    var v = Commit.ParentId;

                    if (v != null)
                        yield return v;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        [DebuggerDisplay($"Count={{{nameof(Count)}}}")]
        private sealed class ParentList : IReadOnlyList<GitCommit>
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            GitCommit Commit { get; }

            public ParentList(GitCommit commit)
            {
                Commit = commit;
                Commit.Read(false);
            }

            public GitCommit this[int index] => Commit.GetParent(index) ?? throw new InvalidOperationException();

            public int Count => Commit.ParentCount;

            public IEnumerator<GitCommit> GetEnumerator()
            {
                if (Commit._parent is object[] parents)
                {
                    for (int i = 0; i < parents.Length; i++)
                    {
                        if (parents[i] is GitCommit c)
                            yield return c;
                        else
                            yield return Commit.GetParent(i)!;
                    }
                }
                else
                {
                    var v = Commit.Parent;

                    if (v != null)
                        yield return v;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        [DebuggerDisplay($"Count={{{nameof(Count)}}}")]
        private sealed class MergeTagList : IReadOnlyList<GitTagObject?>
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            GitCommit Commit { get; }

            public MergeTagList(GitCommit commit)
            {
                Commit = commit;
                Commit.Read(false);
            }

            public GitTagObject? this[int index] => FindMergeTag(Commit.ParentIds[index]);

            private GitTagObject? FindMergeTag(GitId gitId)
                => Commit._mergeTags?.FirstOrDefault(t => t.GitObjectId == gitId);

            public int Count => Commit.ParentCount;

            public IEnumerator<GitTagObject?> GetEnumerator()
                => Commit.ParentIds.Select(x => FindMergeTag(x)).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }

}
