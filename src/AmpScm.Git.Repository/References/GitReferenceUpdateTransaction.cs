namespace AmpScm.Git.References;

public abstract class GitReferenceUpdateTransaction : IDisposable
{
    private protected GitReferenceUpdateTransaction(GitRepository repository)
    {
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    private protected List<GitReferenceUpdateItem> Updates { get; } = new();

    protected GitRepository Repository { get; }

    public string? Reason { get; set; }

    public void Update(string referenceName, GitId newValue, GitId? oldValue = null)
    {
        if (string.IsNullOrWhiteSpace(referenceName))
            throw new ArgumentNullException(nameof(referenceName));
        else if (!GitReference.ValidName(referenceName, allowSpecialSymbols: true))
            throw new ArgumentOutOfRangeException(nameof(referenceName));
        else if (newValue is null)
            throw new ArgumentNullException(nameof(newValue));

        var r = Updates.LastOrDefault(x => x.Name == referenceName);

        if (r is not null && r.Type > UpdateType.Verify)
            throw new GitException($"Can only update '{referenceName}' once inside a transaction");

        if (oldValue is not null)
            Updates.Insert(0, new() { Name = referenceName, Type = UpdateType.Verify, Id = oldValue });

        Updates.Add(new() { Name = referenceName, Type = UpdateType.Update, Id = newValue });
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="newValue"></param>
    /// <param name="oldValue"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void UpdateHead(GitId newValue, GitId? oldValue = null)
    {
        if (newValue is null)
            throw new ArgumentNullException(nameof(newValue));

        Update(GitReferenceRepository.Head, newValue, oldValue);
    }

    public void Create(string referenceName, GitId newValue)
    {
        if (string.IsNullOrWhiteSpace(referenceName))
            throw new ArgumentNullException(nameof(referenceName));
        else if (!GitReference.ValidName(referenceName, allowSpecialSymbols: true))
            throw new ArgumentOutOfRangeException(nameof(referenceName));
        else if (newValue is null)
            throw new ArgumentNullException(nameof(newValue));

        var r = Updates.LastOrDefault(x => x.Name == referenceName);

        if (r is not null && r.Type > UpdateType.Verify)
            throw new GitException($"Can only update '{referenceName}' once inside a transaction");

        Updates.Add(new() { Name = referenceName, Type = UpdateType.Create, Id = newValue });
    }

    public void Delete(string referenceName, GitId? oldValue = null)
    {
        if (string.IsNullOrWhiteSpace(referenceName))
            throw new ArgumentNullException(nameof(referenceName));
        else if (!GitReference.ValidName(referenceName, allowSpecialSymbols: true))
            throw new ArgumentOutOfRangeException(nameof(referenceName));

        var r = Updates.LastOrDefault(x => x.Name == referenceName);

        if (r is not null && r.Type > UpdateType.Verify)
            throw new GitException($"Can only update '{referenceName}' once inside a transaction");

        if (oldValue is not null)
            Updates.Insert(0, new() { Name = referenceName, Type = UpdateType.Verify, Id = oldValue });

        Updates.Add(new() { Name = referenceName, Type = UpdateType.Delete });
    }

    public void Verify(string referenceName, GitId? oldValue = null)
    {
        if (string.IsNullOrWhiteSpace(referenceName))
            throw new ArgumentNullException(nameof(referenceName));
        else if (!GitReference.ValidName(referenceName, allowSpecialSymbols: true))
            throw new ArgumentOutOfRangeException(nameof(referenceName));

        var r = Updates.FirstOrDefault(x => x.Name == referenceName);

        if (r is not null && r.Type == UpdateType.Verify)
            throw new GitException($"Can only verify '{referenceName}' once inside a transaction");

        if (oldValue is not null)
            Updates.Insert(0, new() { Name = referenceName, Type = UpdateType.Verify, Id = oldValue });
    }

    public abstract ValueTask CommitAsync();


    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {

    }

    private protected enum UpdateType
    {
        Verify,
        Create,
        Update,
        Delete,
    }
    private protected record class GitReferenceUpdateItem
    {
        private string? _targetName;
        public string Name { get; set; } = default!;
        public UpdateType Type { get; init; }
        public GitId? Id { get; init; }
        public string TargetName
        {
            get => _targetName ?? Name;
            set => _targetName = value;
        }
    }
}
