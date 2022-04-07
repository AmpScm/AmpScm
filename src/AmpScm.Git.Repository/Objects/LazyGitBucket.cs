using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Git.Objects
{
    internal sealed class LazyGitObjectBucket : GitObjectBucket, IBucketPoll
    {
        GitRepository Repository { get; }
        GitId Id { get; }
        GitObjectBucket? _inner;
        public LazyGitObjectBucket(GitRepository repository, GitId id, GitObjectType type=GitObjectType.None) : base(Bucket.Empty)
        {
            Repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Type = type;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            if (_inner == null)
                _inner = await Repository.ObjectRepository.ResolveById(Id).ConfigureAwait(false) ?? throw new InvalidOperationException($"Can't fetch {Id}");

            var bb =  await _inner.ReadAsync(requested).ConfigureAwait(false);

            if (Type == GitObjectType.None)
                Type = _inner.Type;
            return bb;
        }

        public override async ValueTask ReadTypeAsync()
        {
            if (Type != GitObjectType.None)
                return;

            if (_inner == null)
                _inner = await Repository.ObjectRepository.ResolveById(Id).ConfigureAwait(false) ?? throw new InvalidOperationException($"Can't fetch {Id}");

            await _inner.ReadTypeAsync().ConfigureAwait(false);
            Type = _inner.Type;
        }

        public override BucketBytes Peek()
        {
            return _inner?.Peek() ?? BucketBytes.Empty;
        }

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            if (_inner is null)
                return null;
            else
                return await _inner.ReadRemainingBytesAsync().ConfigureAwait(false);
        }

        public override long? Position => _inner?.Position;

        public override bool CanReset => _inner?.CanReset ?? true;

        public override ValueTask ResetAsync()
        {
            if (_inner != null)
                return _inner.ResetAsync();
            else
                return default;
        }

        public override ValueTask<Bucket> DuplicateAsync(bool reset)
        {
            if (_inner != null)
                return _inner.DuplicateAsync(reset);

            return base.DuplicateAsync(reset);
        }

        public override ValueTask<long> ReadSkipAsync(long requested)
        {
            if (_inner != null)
                return _inner.ReadSkipAsync(requested);

            return base.ReadSkipAsync(requested);
        }

        public override ValueTask<(BucketBytes, BucketEol)> ReadUntilEolAsync(BucketEol acceptableEols, int requested = int.MaxValue)
        {
            if (_inner != null)
                return _inner.ReadUntilEolAsync(acceptableEols, requested);

            return base.ReadUntilEolAsync(acceptableEols, requested);
        }

        public ValueTask<BucketBytes> PollAsync(int minRequested = 1)
        {
            if (_inner != null)
                return _inner.PollAsync(minRequested);

            return new ValueTask<BucketBytes>(Peek());
        }

        public override string Name => _inner?.Name ?? base.Name;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner?.Dispose();

            base.Dispose(disposing);
        }
    }
}
