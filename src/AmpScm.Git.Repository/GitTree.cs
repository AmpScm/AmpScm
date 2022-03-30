﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Git.Objects;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Implementation;
using AmpScm.Git.Objects;
using AmpScm.Git.Sets;

namespace AmpScm.Git
{
    public sealed class GitTree : GitObject, IEnumerable<GitTreeEntry>, IAsyncEnumerable<GitTreeEntry>, IGitLazy<GitTree>
    {
        List<GitTreeEntry> _entries = new List<GitTreeEntry>();
        private GitBucket? _rdr;

        internal GitTree(GitRepository repository, GitId id)
            : base(repository, id)
        {
        }

        internal GitTree(GitRepository repository, GitBucket rdr, GitId id)
            : this(repository, id)
        {
            _rdr = rdr;
        }

        public override GitObjectType Type => GitObjectType.Tree;

        private async ValueTask ReadNext()
        {
            if (_rdr == null)
                return;

            if (_rdr is not GitTreeReadBucket rdr)
            {
                _rdr = rdr = new GitTreeReadBucket(_rdr, Repository.InternalConfig.IdType);
            }

            var el = await rdr.ReadTreeElementRecord().ConfigureAwait(false);

            if (el is null)
            {
                _rdr = null;
                rdr.Dispose();
                return;
            }

            _entries.Add(NewGitTreeEntry(el));
        }

        private GitTreeEntry NewGitTreeEntry(GitTreeElementRecord value)
        {
            if (value.Type == GitTreeElementType.Directory)
                return new GitDirectoryTreeEntry(this, value.Name, value.Id);
            else
                return new GitFileTreeEntry(this, value.Name, value.Type, value.Id);
        }

        public async IAsyncEnumerator<GitTreeEntry> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            if (_rdr is null)
            {
                foreach (GitTreeEntry entry in _entries)
                    yield return entry;
            }

            int n = 0;
            while (true)
            {
                for (; n < _entries.Count; n++)
                {
                    yield return _entries[n];
                }

                if (n == _entries.Count && _rdr == null)
                {
                    yield break;
                }

                await ReadNext().ConfigureAwait(false);
            }
        }

        public IEnumerator<GitTreeEntry> GetEnumerator()
        {
            if (_rdr is null)
                return _entries.GetEnumerator();

            return this.AsNonAsyncEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        ValueTask<GitId> IGitLazy<GitTree>.WriteToAsync(GitRepository repository)
        {
            if (repository != Repository && !repository.Blobs.ContainsId(Id))
                return this.AsWriter().WriteToAsync(repository);
            else
                return new ValueTask<GitId>(Id);
        }

        GitTreeItemCollection? _allFiles;
        GitTreeItemCollection? _allItems;
        public GitTreeItemCollection AllFiles => _allFiles ??= new GitTreeItemCollection(this, true);

        public GitTreeItemCollection AllItems => _allItems ??= new GitTreeItemCollection(this, false);
    }
}
