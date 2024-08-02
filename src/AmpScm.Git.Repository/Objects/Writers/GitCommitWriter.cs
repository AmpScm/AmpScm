using System.Globalization;
using System.Text;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;

namespace AmpScm.Git.Objects;

public sealed class GitCommitWriter : GitObjectWriter<GitCommit>
{
    private IReadOnlyList<IGitLazy<GitCommit>>? _parents;
    private IReadOnlyList<IGitLazy<GitTagObject>>? _mergeTags;
    private GitSignature? _committer;
    private GitSignature? _author;
    private GitTreeWriter? _tree;
    private string? _message;

    public override GitObjectType Type => GitObjectType.Commit;

    public GitTreeWriter Tree
    {
        get => _tree ??= GitTreeWriter.CreateEmpty();
        set
        {
            _tree = value;
            Id = null;
        }
    }

    public IReadOnlyList<IGitLazy<GitCommit>> Parents
    {
        get => _parents ?? Array.Empty<IGitLazy<GitCommit>>();
        set
        {
            if (value == null)
                _parents = null;
            else if (value.Any(x => x is null))
                throw new ArgumentOutOfRangeException(nameof(value));
            else
                _parents = value.ToArray();
            Id = null;
        }
    }

    public IReadOnlyList<IGitLazy<GitTagObject>> MergeTags
    {
        get => _mergeTags ?? Array.Empty<IGitLazy<GitTagObject>>();
        set
        {
            _mergeTags = value;
            Id = null;
        }
    }

    public GitSignature? Committer
    {
        get => _committer;
        set
        {
            _committer = value;
            Id = null;
        }
    }

    public GitSignature? Author
    {
        get => _author;
        set
        {
            _author = value;
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

    private GitCommitWriter()
    {
    }

    public static GitCommitWriter Create(params IGitLazy<GitCommit>[] parents)
    {
        if (parents?.Any(x => x == null) ?? false)
            throw new ArgumentOutOfRangeException(nameof(parents));

        return new GitCommitWriter()
        {
            Parents = parents?.ToArray() ?? Array.Empty<IGitLazy<GitCommit>>(),
            Tree = (parents?.FirstOrDefault() is IGitLazy<GitCommit> gc ? (gc as GitCommit)?.Tree.AsWriter() ?? (gc as GitCommitWriter)?.Tree : null) ?? GitTreeWriter.CreateEmpty()
        };
    }

    public static GitCommitWriter CreateFromTree(IGitLazy<GitTree> tree)
    {
        if (tree is null)
            throw new ArgumentNullException(nameof(tree));

        return new GitCommitWriter()
        {
            Tree = (tree as GitTreeWriter) ?? (tree as GitTree)?.AsWriter() ?? throw new ArgumentNullException(nameof(tree))
        };
    }

    public override async ValueTask<GitId> WriteToAsync(GitRepository repository)
    {
        if (repository is null)
            throw new ArgumentNullException(nameof(repository));

        if (Id is null || !repository.Commits.ContainsId(Id))
        {
            Bucket result;

            StringBuilder sb = new StringBuilder();

            var id = await Tree.WriteToAsync(repository).ConfigureAwait(false);

            sb.Append(CultureInfo.InvariantCulture, $"tree {id}\n");

            HashSet<GitId> parentIds = new();
            foreach (var p in Parents)
            {
                id = await p.WriteToAsync(repository).ConfigureAwait(false);
                sb.Append(CultureInfo.InvariantCulture, $"parent {id}\n");

                parentIds.Add(id);
            }

            var committer = Committer ?? repository.Configuration.Identity;
            var author = Author ?? repository.Configuration.Identity;

            sb.Append(CultureInfo.InvariantCulture, $"author {author.AsRecord()}\n");
            sb.Append(CultureInfo.InvariantCulture, $"committer {committer.AsRecord()}\n");

            result = Bucket.Create.FromUTF8(sb.ToString());

            foreach (var mt in MergeTags)
            {
                IGitLazy<GitTagObject> tag = mt;
                GitId tagId;

                if (mt is GitTagObjectWriter tagWriter)
                {
                    tagId = await mt.WriteToAsync(repository).ConfigureAwait(false);
                    tag = await repository.TagObjects.GetAsync(tagId).ConfigureAwait(false) ?? throw new InvalidOperationException();
                }

                if (tag is GitTagObject tagOb)
                {
                    if (tagOb.ObjectType != GitObjectType.Commit)
                        throw new InvalidOperationException($"Mergetag {id} doesn't refer to a Commit");

                    if (!parentIds.Contains(tagOb.GitObjectId))
                        throw new InvalidOperationException($"Mergetag {id} doesn't refer to a Parent");

                    tagId = tagOb.Id;
                }
                else
                    throw new NotSupportedException();

                Bucket obBucket = await repository.ObjectRepository.ResolveById(tagId).ConfigureAwait(false) ?? throw new InvalidOperationException();

                result += Bucket.Create.FromUTF8("mergetag")
                    + new GitLineIndentBucket(obBucket);
            }

            sb = new StringBuilder();
            // "encoding " // if not UTF-8
            // -extra headers-
            sb.Append('\n');

            var msg = Message;
            if (!string.IsNullOrWhiteSpace(msg))
                sb.Append(msg.Replace("\r", "", StringComparison.Ordinal));

            result += Bucket.Create.FromUTF8(sb.ToString());

            Id = await WriteBucketAsObject(result, repository).ConfigureAwait(false);
        }
        return Id;
    }
}
