using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Git.Client.Porcelain
{
    public enum GitStatusUntrackedMode
    {
        None,
        Normal,
        DirectoriesOnly,
        Recursive
    }
    public enum GitStatusType
    {
        None,
        Modified,
        Added,
        Deleted,
        Renamed,
        Copied,
        Unmerged,
        TypeChanged,
    }

    public class GitStatusArgs : GitPorcelainArgs
    {
        public GitStatusUntrackedMode Untracked { get; set; }
        public override void Verify()
        {
            //throw new NotImplementedException();
        }
    }

    public record class GitStatusEntry
    {
        public string Name { get; init; } = default!;

        public GitStatusType DirectoryChange { get; init; }
        public GitStatusType WorkTreeChange { get; init; }

        public GitTreeElementType HeadType { get; init; }
        public GitTreeElementType DirectoryType { get; init; }
        public GitTreeElementType WorkTreeType { get; init; }

        public GitId? OriginalId { get; init; }
        public GitId? Id { get; init; }

    }

    public partial class GitPorcelain
    {
        [GitCommand("status")]
        public static ValueTask<IAsyncEnumerable<GitStatusEntry>> Status(this GitPorcelainClient c, IEnumerable<string>? paths, GitStatusArgs? options = null)
        {
            options?.Verify();
            options ??= new();

            List<string> args = new();

            if (options.Untracked == GitStatusUntrackedMode.Normal)
                args.Add("--untracked=normal");
            else if (options.Untracked == GitStatusUntrackedMode.Recursive)
                args.Add("--untracked=all");
            else if (options.Untracked == GitStatusUntrackedMode.DirectoriesOnly)
                args.Add("--untracked=no");

            args.Add("--porcelain=v2");

            if (paths?.Any() ?? false)
            {
                args.Add("--");
                args.AddRange(paths);
            }

            return new(WalkStatusResults(c.Repository, c.Repository.WalkPlumbingCommand("status", args)));
        }

        [GitCommand("status")]
        public static async ValueTask<IAsyncEnumerable<GitStatusEntry>> Status(this GitPorcelainClient c, GitStatusArgs? options = null)
        {
            return await Status(c, paths: null, options);
        }

        private static async IAsyncEnumerable<GitStatusEntry> WalkStatusResults(GitRepository r, IAsyncEnumerable<string> results)
        {
            int nameOffset = 33 + r.InternalConfig.IdType.HashLength() * 4;

            await foreach (var line in results)
            {
                if (line.Length == 0)
                    continue;
                switch (line[0])
                {
                    case '#':
                        continue; // Special
                    case '1':
                        {
                            GitId? idOrig, idNew;

                            GitId.TryParse(line.Substring(31, r.InternalConfig.IdType.HashLength() * 2), out idOrig);
                            GitId.TryParse(line.Substring(32 + r.InternalConfig.IdType.HashLength() * 2, r.InternalConfig.IdType.HashLength() * 2), out idNew);
                            yield return new GitStatusEntry
                            {
                                Name = line.Substring(nameOffset),
                                DirectoryChange = GetChange(line[2]),
                                WorkTreeChange = GetChange(line[3]),
                                OriginalId = (idOrig?.IsZero ?? true) ? null : idOrig,
                                Id = (idNew?.IsZero ?? true) ? null : idNew,
                                HeadType = (GitTreeElementType)Convert.ToInt32(line.Substring(10, 6), 8),
                                DirectoryType = (GitTreeElementType)Convert.ToInt32(line.Substring(17, 6), 8),
                                WorkTreeType = (GitTreeElementType)Convert.ToInt32(line.Substring(24, 6), 8),
                            };
                        }
                        // Normal
                        break;
                    case '2':
                        // Renamed or copied
                        break;
                    case 'u':
                        // Unmerged
                        break;
                    case '?':
                        // Untracked
                        break;
                    case '!':
                        // Ignored
                        break;
                    default:
                        throw new NotImplementedException();
                }

#if DEBUG
                Debug.WriteLine(line);
#endif
            }
        }

        private static GitStatusType GetChange(char v)
            => v switch
            {
                'M' => GitStatusType.Modified,
                'T' => GitStatusType.TypeChanged,
                'A' => GitStatusType.Added,
                'D' => GitStatusType.Deleted,
                'R' => GitStatusType.Renamed,
                'C' => GitStatusType.Copied,
                'U' => GitStatusType.Unmerged,
                '.' or _ => GitStatusType.None
            };
    }
}
