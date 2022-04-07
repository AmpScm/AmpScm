﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Git.Objects;

namespace AmpScm.Git
{
    public sealed class GitBlob : GitObject, IGitLazy<GitBlob>
    {
        private GitBucket? _rdr;
        long? _length;

        public sealed override GitObjectType Type => GitObjectType.Blob;

        internal GitBlob(GitRepository repository, GitBucket rdr, GitId id)
            : base(repository, id)
        {
            _rdr = rdr;
        }

        internal Bucket GetBucket()
        {
            return Repository.ObjectRepository.ResolveById(Id).AsTask().GetAwaiter().GetResult()!;
        }

        ValueTask<GitId> IGitLazy<GitBlob>.WriteToAsync(GitRepository repository)
        {
            if (repository != Repository && !repository.Blobs.ContainsId(Id))
                return this.AsWriter().WriteToAsync(repository);
            else
                return new ValueTask<GitId>(Id);
        }

        public long Size
        {
            get
            {
                if (!_length.HasValue)
                    ReadAsync().AsTask().GetAwaiter().GetResult();

                return _length ?? 0;
            }
        }

        public override async ValueTask ReadAsync()
        {
            if (_rdr == null)
                return;

            _length ??= await _rdr.ReadRemainingBytesAsync().ConfigureAwait(false);

            _rdr.Dispose();
            _rdr = null;
        }

        public Bucket AsBucket()
        {
            return GetBucket();
        }

        public Stream AsStream()
        {
            return GetBucket().AsStream();
        }
    }
}
