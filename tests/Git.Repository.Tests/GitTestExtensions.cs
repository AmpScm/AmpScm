using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git;
using AmpScm.Git.Objects;

namespace GitRepositoryTests
{
    internal static class GitTestExtensions
    {
        public static async ValueTask<TGitObject> WriteAndFetchAsync<TGitObject>(this GitObjectWriter<TGitObject> writer, GitRepository repository)
            where TGitObject : GitObject
        {
            if (writer is null)
                throw new ArgumentNullException(nameof(writer));

            if (repository is null)
                throw new ArgumentNullException(nameof(repository));

            var id = await writer.WriteToAsync(repository).ConfigureAwait(false);
            return (await repository.Objects.GetAsync(id).ConfigureAwait(false) as TGitObject) ?? throw new InvalidOperationException();
        }

    }
}
