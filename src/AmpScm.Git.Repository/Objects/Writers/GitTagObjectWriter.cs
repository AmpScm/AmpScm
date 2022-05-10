using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;

namespace AmpScm.Git.Objects
{
    public class GitTagObjectWriter : GitObjectWriter<GitTagObject>
    {
        string? _name;
        string? _message;
        GitSignature? _tagger;
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

        public string? TagMessage
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

                sb.Append((string)$"object {id}\n");
#pragma warning disable CA1308 // Normalize strings to uppercase
                sb.Append((string)$"type {GitObject.Type.ToString().ToLowerInvariant()}\n");
#pragma warning restore CA1308 // Normalize strings to uppercase
                sb.Append((string)$"tag {Name}\n");

                var tagger = Tagger ?? repository.Configuration.Identity;

                sb.Append((string)$"tagger {tagger.AsRecord()}\n");
                // -extra headers-
                sb.Append('\n');

                var msg = TagMessage;
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
}
