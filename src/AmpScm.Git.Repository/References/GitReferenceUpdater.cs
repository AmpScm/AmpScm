using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Git;
using AmpScm.Git.Repository;

namespace AmpScm.Git.References
{
    internal sealed class GitReferenceUpdater : GitReferenceUpdateTransaction
    {
        private GitReferenceRepository _referenceRepository;

        public GitReferenceUpdater(GitReferenceRepository referenceRepository)
            : base(referenceRepository.Repository)
        {
            _referenceRepository = referenceRepository;
        }

        public override async ValueTask CommitAsync()
        {
            if (!string.IsNullOrEmpty(GitConfiguration.GitProgramPath)
                && (await Repository.Configuration.GetBoolAsync("ampscm", "git-update-ref").ConfigureAwait(false) ?? true))
            {
                await CommitUpdateViaGit().ConfigureAwait(false);
            }
            else
            {
                await CommitUpdateFileReferences().ConfigureAwait(false);
            }
        }

        private async ValueTask CommitUpdateFileReferences()
        {
            foreach (var v in Updates.Where(x => x.Type == UpdateType.Verify))
            {
                var r = await _referenceRepository.GetAsync(v.Name).ConfigureAwait(false);

                if (r is null || r.Id != v.Id)
                    throw new GitException($"Reference {v.Name} is not {v.Id}, but {r?.Id ?? Zero}");
            }

            Action? unlock = null;
            string? hookData = null;
            try
            {
                foreach (var v in Updates.Select(x => x.Name).Distinct())
                {
                    var name = v;
                    if (GitReference.AllUpper(name))
                    {
                        var rf = await _referenceRepository.GetAsync(v).ConfigureAwait(false);
                        if (rf is GitSymbolicReference sr)
                        {
                            rf = await sr.ResolveAsync().ConfigureAwait(false);
                            name = (rf as GitSymbolicReference)?.ReferenceName ?? rf.Name ?? name;
                        }
                    }

                    string path = Path.Combine(Repository.GitDir, name);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                    string p = path + ".lock";
#pragma warning disable CA2000 // Dispose objects before losing scope
                    var f = new FileStream(path + ".lock", FileMode.CreateNew);
#pragma warning restore CA2000 // Dispose objects before losing scope

                    unlock += () => { f.Close(); File.Delete(p); };                   
                }


                bool allowContinue = true;

                if (!string.IsNullOrEmpty(GitConfiguration.GitProgramPath)
                    && await Repository.Configuration.HookExistsAsync("reference-transaction").ConfigureAwait(false))
                {
                    StringBuilder sb = new StringBuilder();

                    foreach (var v in Updates)
                    {
                        GitReference? rf;

                        // We might record 'HEAD' when we really update something like 'refs/heads/main'
                        // This might need fixing when things are fixed in git itself
                        switch (v.Type)
                        {
                            case UpdateType.Create:
                                sb.Append(Zero);
                                sb.Append(' ');
                                sb.Append(v.Id);
                                sb.Append(' ');
                                sb.Append(v.Name);
                                sb.Append('\n');
                                break;
                            case UpdateType.Update:
                                rf = await _referenceRepository.GetAsync(v.Name).ConfigureAwait(false);
                                sb.Append(rf?.Id ?? Zero);
                                sb.Append(' ');
                                sb.Append(v.Id);
                                sb.Append(' ');
                                sb.Append(v.Name);
                                sb.Append('\n');
                                break;
                            case UpdateType.Delete:
                                rf = await _referenceRepository.GetAsync(v.Name).ConfigureAwait(false);
                                if (rf?.Id != null)
                                {
                                    sb.Append(rf.Id);
                                    sb.Append(' ');
                                    sb.Append(Zero);
                                    sb.Append(' ');
                                    sb.Append(v.Name);
                                    sb.Append('\n');
                                }
                                break;
                        }
                    }

                    if (sb.Length > 0)
                        hookData = sb.ToString();

                    if (hookData is not null)
                    {
                        var r = await Repository.RunHookErr("reference-transaction", new[] {"prepared" }, stdinText: hookData, expectedResults: Array.Empty<int>()).ConfigureAwait(false);

                        if (r.ExitCode != 0)
                        {
                            throw new GitException($"Git reference-transaction denied update: {r.OutputText} ({r.ErrorText})");
                        }
                    }
                }

                if (allowContinue)
                {
                    var signature = Repository.Configuration.Identity.AsRecord();
                    foreach (var v in Updates)
                    {
                        GitId? originalId = null;
                        GitReference? rf = null;
                        switch (v.Type)
                        {
                            case UpdateType.Create:
                                using (var fs = new FileStream(Path.Combine(Repository.GitDir, v.TargetName), FileMode.CreateNew))
                                using (var sw = new StreamWriter(fs))
                                {
                                    await sw.WriteLineAsync(v.Id!.ToString()).ConfigureAwait(false);
                                }
                                break;
                            case UpdateType.Update:
                                rf = await _referenceRepository.GetAsync(v.Name).ConfigureAwait(false);
                                if (rf is GitSymbolicReference sr)
                                {
                                    rf = await sr.ResolveAsync().ConfigureAwait(false);
                                    v.TargetName = (rf as GitSymbolicReference)?.ReferenceName ?? rf.Name ?? v.Name;
                                }
                                originalId = rf?.Id;
                                using (var fs = new FileStream(Path.Combine(Repository.GitDir, v.TargetName), FileMode.Create))
                                using (var sw = new StreamWriter(fs))
                                {
                                    fs.SetLength(0);
                                    await sw.WriteLineAsync(v.Id!.ToString()).ConfigureAwait(false);
                                }
                                break;
                            case UpdateType.Delete:
                                rf = await _referenceRepository.GetAsync(v.Name).ConfigureAwait(false);
                                if (rf is GitSymbolicReference sr2)
                                {
                                    rf = await sr2.ResolveAsync().ConfigureAwait(false);
                                    v.TargetName = rf.Name ?? v.Name;
                                }
                                originalId = rf?.Id;
                                File.Delete(Path.Combine(Repository.GitDir, v.TargetName));
                                // If failed here, we need to cleanup packed references!!
                                break;
                            default:
                                continue;
                        }


                        var log = new GitReferenceLogRecord { Original = originalId ?? Zero, Target = v.Id ?? Zero, Signature = signature, Reason = Reason };

                        await AppendLog(v.Name, log).ConfigureAwait(false);

                        if (rf is not null && rf.Name != v.Name)
                            await AppendLog(rf.Name!, log).ConfigureAwait(false);
                    }

                    if (hookData is not null)
                    {
                        var hd = hookData;
                        hookData = null;
                        // Ignore errors
                        await Repository.RunHookErr("run", new[] { "committed" }, stdinText: hd, expectedResults: Array.Empty<int>()).ConfigureAwait(false);
                    }
                }
            }
            catch when (hookData is not null)
            {
                // Ignore errors
                await Repository.RunHookErr("run", new[] { "abort" }, stdinText: hookData, expectedResults: Array.Empty<int>()).ConfigureAwait(false);
                throw;
            }
            finally
            {
                unlock?.Invoke();
            }
        }

        private Task AppendLog(string name, GitReferenceLogRecord log)
        {
            name = "logs/" + name;

            string path = Path.Combine(Repository.GitDir, name)!;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
#if !NETFRAMEWORK
            return File.AppendAllTextAsync(path, log.ToString());
#else
            File.AppendAllText(path, log.ToString());
            return Task.CompletedTask;
#endif
        }

        GitId? _zero;
        GitId Zero
        {
            get => _zero ??= new GitId(Repository.InternalConfig.IdType, new byte[Repository.InternalConfig.IdType.HashLength()]);
        }

        private async ValueTask CommitUpdateViaGit()
        {
            StringBuilder sb = new StringBuilder();

            var names = Updates.Select(x => x.Name).Distinct().ToArray();

            if (names.Length == 0)
                return;

            if (names.Length == 1)
            {
                if (Updates.Count <= 2 && Updates.Last().Type == UpdateType.Update)
                {
                    // No transaction in this minimal case
                    sb.Append("update ");
                    sb.Append(names[0]);
                    sb.Append('\0');
                    sb.Append(Updates.Last().Id);
                    sb.Append('\0');
                    if (Updates.Count > 1 && Updates[0].Type == UpdateType.Verify)
                        sb.Append(Updates[0].Id);
                    sb.Append('\0');
                }
                else if (Updates.Count == 1 && Updates[0].Type == UpdateType.Create)
                {
                    sb.Append("create ");
                    sb.Append(names[0]);
                    sb.Append('\0');
                    sb.Append(Updates[0].Id);
                    sb.Append('\0');
                }
            }

            if (sb.Length == 0)
            {
                sb.Append("start\0");
                foreach (var v in Updates)
                {
                    switch (v.Type)
                    {
                        case UpdateType.Verify:
                            sb.Append("verify ");
                            sb.Append(v.Name);
                            sb.Append('\0');
                            sb.Append(v.Id);
                            sb.Append('\0');
                            break;
                        case UpdateType.Create:
                            sb.Append("create ");
                            sb.Append(v.Name);
                            sb.Append('\0');
                            sb.Append(v.Id);
                            sb.Append('\0');
                            break;
                        case UpdateType.Delete:
                            sb.Append("delete ");
                            sb.Append(v.Name);
                            sb.Append('\0');
                            sb.Append('\0');
                            break;
                        case UpdateType.Update:
                            sb.Append("update ");
                            sb.Append(v.Name);
                            sb.Append('\0');
                            sb.Append(v.Id);
                            sb.Append('\0');
                            sb.Append('\0');
                            break;
                    }
                }
                sb.Append("prepare\0");
                sb.Append("commit\0");
            }

            List<string> args = new() { "--stdin", "-z" };
            if (!string.IsNullOrWhiteSpace(Reason))
            {
                args.AddRange(new[] { "-m", Reason! });
            }

            await Repository.RunPlumbingCommandOut("update-ref", args.ToArray(), sb.ToString()).ConfigureAwait(false);
        }
    }
}
