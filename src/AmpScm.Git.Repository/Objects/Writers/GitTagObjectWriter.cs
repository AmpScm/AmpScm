using System.Globalization;
using System.Text;
using AmpScm.Buckets;

namespace AmpScm.Git.Objects;

public class GitTagObjectWriter : GitObjectWriter<GitTagObject>
{
    private string? _name;
    private string? _message;
    private GitSignature? _tagger;
    public IGitLazy<GitObject> GitObject { get; set; } = default!;

    private GitTagObjectWriter()
    {

    }
    public sealed override GitObjectType Type => GitObjectType.Tag;

    public string Name
    {
        get => _name ?? "v0";
        set
        {
            _name = value;
            Id = null;
        }
    }

    public string? Message
    {
        get => _message;
        set
        {
            _message = value;
            Id = null;
        }
    }

    public GitSignature? Tagger
    {
        get => _tagger;
        set
        {
            _tagger = value;
            Id = null;
        }
    }

    public override async ValueTask<GitId> WriteToAsync(GitRepository repository)
    {
        if (repository is null)
            throw new ArgumentNullException(nameof(repository));
        else if (GitObject is null)
            throw new InvalidOperationException();

        if (Id is null || !repository.Commits.ContainsId(Id))
        {
            StringBuilder sb = new StringBuilder();

            var id = await GitObject.WriteToAsync(repository).ConfigureAwait(false);

            sb.Append(CultureInfo.InvariantCulture, $"object {id}\n");
#pragma warning disable CA1308 // Normalize strings to uppercase
            sb.Append(CultureInfo.InvariantCulture, $"type {GitObject.Type.ToString().ToLowerInvariant()}\n");
#pragma warning restore CA1308 // Normalize strings to uppercase
            sb.Append(CultureInfo.InvariantCulture, $"tag {Name}\n");

            var tagger = Tagger ?? repository.Configuration.Identity;

            sb.Append(CultureInfo.InvariantCulture, $"tagger {tagger.AsRecord()}\n");
            // -extra headers-
            sb.Append('\n');

            var msg = Message;
            if (!string.IsNullOrWhiteSpace(msg))
                sb.Append(msg.Replace("\r", "", StringComparison.Ordinal));

            var b = Bucket.Create.FromUTF8(sb.ToString());

            Id = await WriteBucketAsObject(b, repository).ConfigureAwait(false);
        }
        return Id;
    }

    public static GitTagObjectWriter Create(IGitLazy<GitObject> objectToTag, string name)
    {
        if (objectToTag is null)
            throw new ArgumentNullException(nameof(objectToTag));
        else if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(name);

        return new GitTagObjectWriter { GitObject = objectToTag, Name = name };
    }
}
