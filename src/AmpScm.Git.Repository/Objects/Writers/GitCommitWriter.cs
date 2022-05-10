using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;

namespace AmpScm.Git.Objects
{
    public sealed class GitCommitWriter : GitObjectWriter<GitCommit>
    {
        IReadOnlyList<IGitLazy<GitCommit>>? _parents;
        GitSignature? _committer;
        GitSignature? _author;
        GitTreeWriter? _tree;
        string? _message;

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
                StringBuilder sb = new StringBuilder();

                var id = await Tree.WriteToAsync(repository).ConfigureAwait(false);

                sb.Append((string)$"tree {id}\n");

                foreach (var p in Parents)
                {
                    id = await p.WriteToAsync(repository).ConfigureAwait(false);
                    sb.Append((string)$"parent {id}\n");
                }

                var committer = Committer ?? repository.Configuration.Identity;
                var author = Author ?? repository.Configuration.Identity;

                sb.Append((string)$"author {author.AsRecord()}\n");
                sb.Append((string)$"committer {committer.AsRecord()}\n");
                // "encoding " // if not UTF-8
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
    }
}
