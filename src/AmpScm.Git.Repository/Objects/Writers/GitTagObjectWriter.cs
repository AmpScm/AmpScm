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
        public IGitLazy<GitObject> GitObject { get; set; } = default!;

        private GitTagObjectWriter()
        {

        }
        public sealed override GitObjectType Type => GitObjectType.Tag;

        public string Name { get; set; } = "";

        public string? TagMessage { get; set; }

        public GitSignature? Tagger { get; set; }

        public override async ValueTask<GitId> WriteToAsync(GitRepository repository)
        {
            if (repository is null)
                throw new ArgumentNullException(nameof(repository));

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

                var b = Encoding.UTF8.GetBytes(sb.ToString()).AsBucket();

                Id = await WriteBucketAsObject(b, repository).ConfigureAwait(false);
            }
            return Id;
        }

        public static GitTagObjectWriter Create(IGitLazy<GitObject> objectToTag, string name)
        {
            if (objectToTag is null)
                throw new ArgumentNullException(nameof(objectToTag));

            return new GitTagObjectWriter { GitObject = objectToTag, Name = name };
        }
    }
}
