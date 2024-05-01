using AmpScm.Buckets;

namespace AmpScm.Git.Objects;

public sealed class GitBlobWriter : GitObjectWriter<GitBlob>
{
    private Bucket? _bucket;
    private Func<ValueTask<Bucket>>? _fetchBlob;

    private GitBlobWriter(Bucket bucket)
    {
        if (bucket is null)
            throw new ArgumentNullException(nameof(bucket));

        _bucket = bucket;
    }

    public static GitBlobWriter CreateFrom(Bucket bucket)
    {
        if (bucket is null)
            throw new ArgumentNullException(nameof(bucket));

        return new GitBlobWriter(bucket);
    }

    public override async ValueTask<GitId> WriteToAsync(GitRepository repository)
    {
        if (repository is null)
            throw new ArgumentNullException(nameof(repository));

        if (Id is null || !repository.Blobs.ContainsId(Id))
        {
            var bucket = _bucket;
            bucket ??= await _fetchBlob!().ConfigureAwait(false);
            var id = await WriteBucketAsObject(_bucket!, repository).ConfigureAwait(false);

            // We explicitly use the local variable 'id' in the next lambda
            _fetchBlob ??= async () => (await repository.Blobs.GetAsync(id).ConfigureAwait(false))!.GetBucket();
            _bucket = null;

            Id = id;
        }
        return Id;
    }


    public sealed override GitObjectType Type => GitObjectType.Blob;
}
