﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Git.Objects;
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
        GitCommitReadBucket? _rb;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        object? _tree;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        object[]? _parent;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        Dictionary<string, string>? _headers;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string? _message;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string? _summary;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitSignature? _author;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitSignature? _committer;

        internal GitCommit(GitRepository repository, GitObjectBucket objectReader, GitId id)
            : base(repository, id)
        {
            _rb = new GitCommitReadBucket(objectReader);
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

        public string? Message
        {
            get
            {
                if (_message is null)
                    Read(true);

                return _message;
            }
        }

        public string? Summary
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

            _committer ??= new GitSignature(await _rb.ReadCommitter().ConfigureAwait(false));

            //if (!all)
            //    return;

            while (true)
            {
                var (bb, eol) = await _rb.ReadUntilEolFullAsync(BucketEol.LF, null).ConfigureAwait(false);

                if (bb.IsEof || bb.Length == eol.CharCount())
                    break;

                string line = bb.ToUTF8String(eol);

                if (line.Length == 0)
                    break;

                var parts = line.Split(new[] { ' ' }, 2);
                switch (parts[0])
                {
                    case "mergetag":
                        break;

                    case "encoding":
                    case "gpgsig":
                        break; // Ignored for now

                    default:
                        if (!char.IsWhiteSpace(line, 0))
                        {
                            _headers ??= new Dictionary<string, string>();
                            if (_headers.TryGetValue(parts[0], out var v))
                                _headers[parts[0]] = v + "\n" + parts[1];
                            else
                                _headers[parts[0]] = parts[1];
                        }
                        break;
                }
            }

            while (true)
            {
                var (bb, _) = await _rb.ReadUntilEolFullAsync(BucketEol.LF).ConfigureAwait(false);

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
                return new ValueTask<GitId>(Id);
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

        private sealed class IdList : IReadOnlyList<GitId>
        {
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

        private sealed class ParentList : IReadOnlyList<GitCommit>
        {
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

    }

}
