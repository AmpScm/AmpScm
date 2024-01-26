using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Client;
using AmpScm.Buckets.Git;
using AmpScm.Git.Repository.Implementation;

namespace AmpScm.Git.Repository;

public class GitConfiguration : GitBackendRepository
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly string _gitDir;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _loaded;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _repositoryFormatVersion;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly Dictionary<(string Group, string? SubGroup, string Key), string> _config = new();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private static readonly Lazy<string> _gitExePath = new Lazy<string>(GetGitExePath, isThreadSafe: true);
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private static readonly Lazy<string> _homeDir = new Lazy<string>(GetHomeDirectory, isThreadSafe: true);
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public static string GitProgramPath => _gitExePath.Value;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public static Version GitProgramVersion => GitRepository.GitCliVersion;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public static string UserHomeDirectory => _homeDir.Value;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Lazy<GitLazyConfig> _lazy;
    private bool _parseWorkTreeConfig;

    internal GitConfiguration(GitRepository repository, string gitDir)
        : base(repository)
    {
        _gitDir = gitDir;
        _lazy = new Lazy<GitLazyConfig>(() => new GitLazyConfig(this));
    }

    protected override void Dispose(bool disposing)
    {
    }

    internal async ValueTask LoadAsync()
    {
        if (_loaded) return;

        foreach (string path in GetGitConfigurationFilePaths())
        {
            await LoadConfigAsync(path).ConfigureAwait(false);
        }

        try
        {
            await LoadConfigAsync(Path.Combine(_gitDir, "config")).ConfigureAwait(false);

            if (_parseWorkTreeConfig)
            {
                string path = Path.Combine(Repository.WorkTreeDirectory, "config.worktree");

                if (File.Exists(path))
                    await LoadConfigAsync(Path.Combine(Repository.WorkTreeDirectory, "config.worktree")).ConfigureAwait(false);
            }

            var v = Environment.GetEnvironmentVariable("GIT_CONFIG_COUNT");

            if (!string.IsNullOrEmpty(v) && int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var nVars) && nVars > 0)
            {
                for (int i = 0; i < nVars; i++)
                {
                    string? key = Environment.GetEnvironmentVariable($"GIT_CONFIG_KEY_{i}");
                    string? value = Environment.GetEnvironmentVariable($"GIT_CONFIG_VALUE_{i}");

                    if (!string.IsNullOrEmpty(key))
                    {
                        int ns = key.LastIndexOf('.');

                        if (ns > 0)
                        {
                            string group = key.Substring(0, ns);
                            key = key.Substring(ns + 1);

                            int n = group.IndexOf('.', StringComparison.Ordinal);
                            string? subGroup = (n > 0) ? group.Substring(n + 1) : null;
                            group = ((n > 0) ? group.Substring(0, n) : group);

#pragma warning disable CA1308 // Normalize strings to uppercase
                            _config[(group.ToLowerInvariant(), subGroup, key.ToLowerInvariant())] = value ?? "\xFF";
#pragma warning restore CA1308 // Normalize strings to uppercase
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            throw new GitRepositoryException($"Can't open repository config '{Path.Combine(_gitDir, "config")}', GitDir='{Repository.GitDirectory}', FullPath='{Repository.FullPath}'", e);
        }
    }

    private async ValueTask LoadConfigAsync(string path)
    {
        var b = FileBucket.OpenRead(path, forAsync: false);
        using var cr = new GitConfigurationBucket(b);

        while (await cr.ReadRecord().ConfigureAwait(false) is GitConfigurationRecord item)
        {
            _config[(item.Group, item.SubGroup, item.Key)] = item.Value ?? "\xFF";

            if (item.Group == "core" || item.Group == "extension")
                ParseCore(item);
            else if (item.Group == "include")
                await ParseInclude(path, item).ConfigureAwait(false);
            else if (item.Group == "includeif")
                await ParseIncludeIfAsync(path, item).ConfigureAwait(false);
        }
        _loaded = true;
    }

    private async ValueTask ParseInclude(string path, GitConfigurationRecord item)
    {
        if (!(item.SubGroup is var check) || string.IsNullOrEmpty(check))
            return;
        else if (item.Key != "path")
            return; // No other types documented yet

        string newPath = Path.Combine(Path.GetDirectoryName(path)!, ApplyHomeDir(item.Value!));

        if (!string.IsNullOrEmpty(newPath) && File.Exists(newPath))
        {
            await LoadConfigAsync(Path.GetFullPath(newPath)).ConfigureAwait(false);
        }
    }

    private async ValueTask ParseIncludeIfAsync(string path, GitConfigurationRecord item)
    {
        if (!(item.SubGroup is var check) || string.IsNullOrEmpty(check))
            return;
        else if (item.Key != "path" || item.Value == null)
            return; // No other types documented yet


        if (check!.StartsWith("gitdir:", StringComparison.Ordinal)
            || check.StartsWith("gitdir/i:", StringComparison.Ordinal))
        {
            bool caseInsensitive = check[6] == '/';

            string dir = ApplyHomeDir(check.Substring(caseInsensitive ? "gitdir/i:".Length : "gitdir:".Length).Trim());

            if (dir.EndsWith('/'))
                dir += "**";

            if (!GitGlob.Match(dir, Repository.GitDirectory, GitGlobFlags.ParentPath | (caseInsensitive ? GitGlobFlags.CaseInsensitive : GitGlobFlags.None)))
                return;
        }
        else if (check.StartsWith("onbranch:", StringComparison.Ordinal))
        {
            return; // Not supported yet
        }
        else if (check.StartsWith("hasconfig:", StringComparison.Ordinal))
        {
            return; // Not supported yet
        }
        else
            return; // Not supported yet

        string newPath = Path.Combine(Path.GetDirectoryName(path)!, ApplyHomeDir(item.Value!));

        if (!string.IsNullOrEmpty(newPath) && File.Exists(newPath))
        {
            await LoadConfigAsync(Path.GetFullPath(newPath)).ConfigureAwait(false);
        }
    }

    private static string ApplyHomeDir(string path)
    {
        if (path != null && path.StartsWith('~')
            && UserHomeDirectory is var homeDir && !string.IsNullOrWhiteSpace(homeDir))
        {
#pragma warning disable CA1845 // Use span-based 'string.Concat'
            if (path.StartsWith("~/", StringComparison.Ordinal))
                path = homeDir!.TrimEnd(Path.DirectorySeparatorChar) + path.Substring(1);
            else if (char.IsLetterOrDigit(path, 1))
                path = Path.GetDirectoryName(homeDir) + path.Substring(1); // Might need more work on linux, but not common
#pragma warning restore CA1845 // Use span-based 'string.Concat'
        }
        return path!;
    }

    private void ParseCore(GitConfigurationRecord item)
    {
        if (item.Key == "repositoryformatversion" && item.Group == "core")
        {
            if (int.TryParse(item.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
                _repositoryFormatVersion = version;
        }
        else if (item.Key == "worktreeconfig" && item.Group == "extensions")
            _parseWorkTreeConfig = true;
    }

    internal IEnumerable<(string, string)> GetGroup(string group, string? subGroup)
    {
        if (!_loaded)
            LoadAsync().AsTask().Wait();

#pragma warning disable CA1308 // Normalize strings to uppercase
        group = group.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

        foreach (var v in _config)
        {
            var (g, s, k) = v.Key;

            if (group == g && subGroup == s)
                yield return (k, v.Value);
        }
    }

    public IEnumerable<string> GetSubGroups(string group)
    {
        if (string.IsNullOrEmpty(group))
            throw new ArgumentNullException(nameof(group));

        if (!_loaded)
            LoadAsync().AsTask().Wait();

#pragma warning disable CA1308 // Normalize strings to uppercase
        group = group.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
        HashSet<string> subGroups = new HashSet<string>(StringComparer.Ordinal);

        foreach (var v in _config)
        {
            var (g, s, _) = v.Key;

            if (s == null)
                continue;

            if (group == g)
            {
                if (!subGroups.Contains(s))
                {
                    yield return s;
                    subGroups.Add(s);
                }
            }
        }
    }

    internal async ValueTask<GitRemote?> GetRemoteAsync(string name)
    {
        if (await GetStringAsync("remote." + name, "url").ConfigureAwait(false) is string v)
        {
            return new GitRemote(Repository, name, v);
        }

        return null;
    }

    internal async IAsyncEnumerable<GitRemote> GetAllRemotes()
    {
        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

        await LoadAsync().ConfigureAwait(false);

        foreach (var v in _config)
        {
            var (g, s, k) = v.Key;

            if (g != "remote" || s is null)
                continue;

            if (!names.Contains(s))
            {
                yield return new GitRemote(Repository, s, (k == "url") ? v.Value : null);
                names.Add(s);
            }
        }
    }


    public async ValueTask<int?> GetIntAsync(string group, string key)
    {
        if (string.IsNullOrEmpty(group))
            throw new ArgumentNullException(nameof(group));
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        await LoadAsync().ConfigureAwait(false);

        int n = group.IndexOf('.', StringComparison.Ordinal);
        string? subGroup = (n > 0) ? group.Substring(n + 1) : null;
#pragma warning disable CA1308 // Normalize strings to uppercase
        group = ((n > 0) ? group.Substring(0, n) : group).ToLowerInvariant();
        key = key.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

        if (_config.TryGetValue((group, subGroup, key), out var vResult))
        {
            if (int.TryParse(vResult, NumberStyles.None, CultureInfo.InvariantCulture, out var r))
            {
                return r;
            }

            vResult = vResult.TrimEnd();

            if ((vResult.EndsWith('k') || vResult.EndsWith('K') || vResult.EndsWith('m') || vResult.EndsWith('M') || vResult.EndsWith('g') || vResult.EndsWith('G'))
#if NETCOREAPP
                && int.TryParse(vResult.AsSpan(0, vResult.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out r))
#else
                && int.TryParse(vResult.Substring(0, vResult.Length-1), NumberStyles.None, CultureInfo.InvariantCulture, out r))
#endif
            {
                return r * SuffixFactor(vResult[vResult.Length - 1]);
            }
            else
                return null;
        }
        else
            return null;
    }

    private static int SuffixFactor(char v)
        => v switch
        {
            'k' or 'K' => 1024,
            'm' or 'M' => 1024 * 1024,
            'g' or 'G' => 1024 * 1024 * 1024,
            _ => throw new ArgumentOutOfRangeException(nameof(v))
        };

    internal int? GetInt(string group, string key)
    {
        return GetIntAsync(group, key).AsTask().Result;
    }

    public async ValueTask<string?> GetStringAsync(string group, string key)
    {
        if (string.IsNullOrEmpty(group))
            throw new ArgumentNullException(nameof(group));
        else if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        await LoadAsync().ConfigureAwait(false);

        int n = group.IndexOf('.', StringComparison.Ordinal);
        string? subGroup = (n > 0) ? group.Substring(n + 1) : null;
#pragma warning disable CA1308 // Normalize strings to uppercase
        group = ((n > 0) ? group.Substring(0, n) : group).ToLowerInvariant();
        key = key.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

        if (_config.TryGetValue((group, subGroup, key), out var vResult))
        {
            if (vResult == "\xFF")
                return "";
            return vResult;
        }
        else
            return null;
    }

    public async ValueTask<string?> GetPathAsync(string group, string key)
    {
        var value = await GetStringAsync(group, key).ConfigureAwait(false);

        if (value is not null)
            return ApplyHomeDir(value);
        else
            return null;
    }

    internal string? GetString(string group, string key)
    {
        return GetStringAsync(group, key).AsTask().Result;
    }


    public async ValueTask<bool?> GetBoolAsync(string group, string key)
    {
        if (string.IsNullOrEmpty(group))
            throw new ArgumentNullException(nameof(group));
        else if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        await LoadAsync().ConfigureAwait(false);

        int n = group.IndexOf('.', StringComparison.Ordinal);
        string? subGroup = (n > 0) ? group.Substring(n + 1) : null;
#pragma warning disable CA1308 // Normalize strings to uppercase
        group = ((n > 0) ? group.Substring(0, n) : group).ToLowerInvariant();
        key = key.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

        if (_config.TryGetValue((group, subGroup, key), out var vResult))
        {
            // As generated by 'git init'
            if (string.Equals(vResult, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            else if (string.Equals(vResult, "false", StringComparison.OrdinalIgnoreCase))
                return false;

            // The simple no value cases
            else if (vResult == "\xFF" || vResult is null)
                return true;
            else if (vResult.Length == 0)
                return false;

            // And other documented ok
            else if (string.Equals(vResult, "yes", StringComparison.OrdinalIgnoreCase))
                return true;
            else if (string.Equals(vResult, "on", StringComparison.OrdinalIgnoreCase))
                return true;
            else if (string.Equals(vResult, "1", StringComparison.OrdinalIgnoreCase))
                return true;
            else if (string.Equals(vResult, "\xFF", StringComparison.OrdinalIgnoreCase))
                return true;
            else if (string.Equals(vResult, "no", StringComparison.OrdinalIgnoreCase))
                return false;
            else if (string.Equals(vResult, "off", StringComparison.OrdinalIgnoreCase))
                return false;

            else if (string.Equals(vResult, "0", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return null;
    }

    public bool? GetBool(string group, string key)
    {
        return GetBoolAsync(group, key).AsTask().Result;
    }

    internal class GitLazyConfig
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private GitConfiguration Configuration { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Lazy<bool> _repositoryIsLazy;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Lazy<bool> _repositoryIsShallow;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Lazy<bool> _repositoryCommitGraph;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Lazy<bool> _repositorySupportsMultiPack;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly Lazy<int> _autoGCBlobs;

        public GitLazyConfig(GitConfiguration config)
        {
            Configuration = config ?? throw new ArgumentNullException(nameof(config));

            _repositoryIsLazy = new Lazy<bool>(GetRepositoryIsLazy);
            _repositoryIsShallow = new Lazy<bool>(GetRepositoryIsShallow);
            _repositoryCommitGraph = new Lazy<bool>(GetRepositoryCommitGraph);
            _repositorySupportsMultiPack = new Lazy<bool>(GetRepositorySupportsMultiPack);
            _autoGCBlobs = new Lazy<int>(GetAutGCBlobs);
        }

        private bool GetRepositoryIsLazy()
        {
            if (Configuration._loaded && Configuration._repositoryFormatVersion == 0)
                return false;

            foreach (var v in Configuration.GetSubGroups("remote"))
            {
                if (Configuration.GetBool("remote." + v, "promisor") ?? false)
                    return true;
            }

            return false;
        }

        private bool GetRepositoryIsShallow()
        {
            return File.Exists(Path.Combine(Configuration.Repository.GitDirectory, "shallow"));
        }

        private bool GetRepositoryCommitGraph()
        {
            return Configuration.GetBool("core", "commitGraph") ?? true; // By default enabled in git current
        }

        private bool GetRepositorySupportsMultiPack()
        {
            return Configuration.GetBool("core", "multiPackIndex") ?? true; // By default enabled in git current
        }

        private int GetAutGCBlobs()
        {
            return Configuration.GetInt("gc", "auto") ?? 6700;
        }

        public bool RepositoryIsLazy => _repositoryIsLazy.Value;
        public bool RepositoryIsShallow => _repositoryIsShallow.Value;

        public bool CommitGraph => _repositoryCommitGraph.Value;
        public bool MultiPack => _repositorySupportsMultiPack.Value;

        public int AutoGCBlobs => _autoGCBlobs.Value;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal GitLazyConfig Lazy => _lazy.Value;

    private static string GetGitExePath()
    {
        return GitExePathLook() ?? GetExePathWhere() ?? null!;
    }

    private static string? GetExePathWhere()
    {
        try
        {
            var psi = new ProcessStartInfo(Environment.NewLine == "\n" ? "which" : "where", "git")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            string outputText = "";
            using var ps = Process.Start(psi);

            if (ps == null)
                return null;
            ps.StandardInput.Close();
            ps.OutputDataReceived += (sender, e) => outputText += e.Data;
            ps.ErrorDataReceived += (sender, e) => { };
            ps.BeginErrorReadLine();
            ps.BeginOutputReadLine();
            if (ps.WaitForExit(100) && ps.ExitCode == 0)
            {
                string git = outputText.Split('\n', 2)[0].Trim();

                if (File.Exists(git)
                    && File.Exists(git = Path.GetFullPath(git)))
                {
                    return git;
                }
            }

            return null;
        }
        catch (InvalidOperationException)
        { }
        catch (IOException)
        { }
        catch (System.ComponentModel.Win32Exception)
        { }
        return null;
    }

    private static string? GitExePathLook()
    {
        try
        {
            string? path = Environment.GetEnvironmentVariable("PATH");

            if (path == null)
                return null;

            foreach (var p in path.Split(Path.PathSeparator))
            {
                try
                {
                    string git;
                    if (File.Exists(git = Path.Combine(p, "git")))
                        return Path.GetFullPath(git);
                    else if (File.Exists(git = Path.Combine(p, "git.exe")))
                        return Path.GetFullPath(git);
                }
                catch (ArgumentException)
                { }
                catch (IOException)
                { }
                catch (SecurityException)
                { }
            }
        }
        catch (ArgumentException)
        { }
        catch (IOException)
        { }
        catch (SecurityException)
        { }
        return null;
    }

    internal async ValueTask<bool> HookExistsAsync(string hookName)
    {
        string path = await GetPathAsync("core", "hookspath").ConfigureAwait(false) ?? Path.Combine(Repository.GitDirectory, "hooks");

        return File.Exists(Path.Combine(path, hookName));
    }

    public GitSignature Identity
    {
        get
        {
            var username = GetString("user", "name") ?? Environment.UserName ?? "Someone";
            var email = GetString("user", "email") ?? $"me@{Environment.MachineName}.local";

            return new GitSignature(username, email, DateTime.Now);
        }
    }

    private static readonly object _extraHeaderSetTag = new();
#pragma warning disable CA2109 // Review visible event handlers
    public void BasicAuthenticationHandler(object? sender, BasicBucketAuthenticationEventArgs e)
#pragma warning restore CA2109 // Review visible event handlers
    {
        if (e is null)
            throw new ArgumentNullException(nameof(e));

        if (e.Items[_extraHeaderSetTag] is null && (e.Uri?.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            e.Items[_extraHeaderSetTag] = _extraHeaderSetTag;

            // GitHub action uses $ git.exe config --local http.https://github.com/.extraheader "AUTHORIZATION: basic ***"
            var extraHeader = GetString($"http.{e.Uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped)}/", "extraheader") ?? GetString($"http", "extraheader");

            if (!string.IsNullOrEmpty(extraHeader) && extraHeader!.StartsWith("Authorization: Basic ", StringComparison.OrdinalIgnoreCase))
            {
                var p = extraHeader.Split(' ', 3)[2];

                try
                {
                    var userPass = Encoding.UTF8.GetString(Convert.FromBase64String(p));

                    string[] parts = userPass.Split(':', 2);

                    e.Username = parts[0];
                    e.Password = parts[1];
                    e.Continue = true; // If failed, fall through in next code
                    e.Handled = true;
                    return;
                }
                catch (FormatException)
                {
                    // Fall through to normal auth
                }
                catch (DecoderFallbackException)
                {
                    // Fall through to normal auth
                }
                catch (ArgumentException)
                {
                    // Fall through to normal auth
                }
            }
        }

        e.Continue = false; // Only run this handler once!

        // BUG: Somehow the first line gets corrupted, so we write an ignored first line to make sure the required fields get through correctly

#pragma warning disable CA1861 // Avoid constant arrays as arguments
        var r = Repository.RunGitCommandOutAsync("credential", new[] { "fill" }, stdinText: $"url={e.Uri}").AsTask();

        var (exitCode, output) = r.GetAwaiter().GetResult();
        bool gotUser = false;
        bool gotPass = false;
        string? username = null;
        string? password = null;

        foreach (var l in output.Split('\n'))
        {
            var kv = l.Split('=', 2);

            if ("username".Equals(kv[0], StringComparison.OrdinalIgnoreCase))
            {
                username = kv[1].TrimEnd();
                gotUser = true;
            }
            else if ("password".Equals(kv[0], StringComparison.OrdinalIgnoreCase))
            {
                password = kv[1].TrimEnd();
                gotPass = true;
            }
        }

        if (!gotUser || !gotPass || (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password)))
        {
            e.Handled = false;
        }
        else
        {
            e.Username = username;
            e.Password = password;
            e.Succeeded += async (_, _) => await Repository.RunGitCommandAsync("credential", new[] { "approve" }, stdinText: $"url={e.Uri}\nusername={username}\npassword={password}\n").ConfigureAwait(false);
            e.Failed += async (_, _) => await Repository.RunGitCommandAsync("credential", new[] { "reject" }, stdinText: $"url={e.Uri}\nusername={username}\npassword={password}\n").ConfigureAwait(false);
        }
#pragma warning restore CA1861 // Avoid constant arrays as arguments
    }

    public static IEnumerable<string> GetGitConfigurationFilePaths(bool includeSystem = true)
    {
        string f;
        if (includeSystem && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GIT_CONFIG_NOSYSTEM")))
        {
            if (Environment.GetEnvironmentVariable("GIT_CONFIG_SYSTEM") is var gitConfigSystem
                && !string.IsNullOrWhiteSpace(gitConfigSystem) && File.Exists(gitConfigSystem))
            {
                yield return Path.GetFullPath(gitConfigSystem);
            }
            else if (GitProgramPath != null)
            {
                string dir = Path.GetDirectoryName(GitProgramPath)!;

                if (Path.GetDirectoryName(dir) is var parent && File.Exists(f = Path.Combine(parent!, "etc", "gitconfig")))
                    yield return Path.GetFullPath(f);
                else if (Path.GetDirectoryName(parent) is var parent2 && File.Exists(f = Path.Combine(parent2!, "etc", "gitconfig")))
                    yield return Path.GetFullPath(f);

            }
            else if (includeSystem && Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) is var programFiles)
            {
                if (File.Exists(f = Path.Combine(programFiles, "git", "etc", "gitconfig")))
                    yield return Path.GetFullPath(f);
            }

            if (includeSystem && Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) is var commonAppData)
            {
                if (File.Exists(f = Path.Combine(commonAppData, "git", "gitconfig")))
                    yield return Path.GetFullPath(f);
            }
        }

        if (Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL") is var gitConfigGlobal
                && !string.IsNullOrWhiteSpace(gitConfigGlobal) && File.Exists(gitConfigGlobal))
        {
            yield return Path.GetFullPath(gitConfigGlobal);
        }
        else if (UserHomeDirectory is string home && !string.IsNullOrWhiteSpace(UserHomeDirectory) && File.Exists(f = Path.Combine(home, ".gitconfig")))
        {
            yield return Path.GetFullPath(f);
        }
        else if (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) is var localAppData
            && File.Exists(f = Path.Combine(localAppData, "git", "gitconfig")))
        {
            yield return Path.GetFullPath(f);
        }
        else if (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) is var appData
            && File.Exists(f = Path.Combine(appData, "git", "gitconfig")))
        {
            yield return Path.GetFullPath(f);
        }

    }

    private static string GetHomeDirectory()
    {
        if (Environment.GetEnvironmentVariable("HOME") is string home
            && !string.IsNullOrWhiteSpace(home) && Directory.Exists(home))
        {
            return Path.GetFullPath(home);
        }

        if (Environment.GetEnvironmentVariable("HOMEDRIVE") is var homeDrive
            && Environment.GetEnvironmentVariable("HOMEPATH") is var homePath
            && !string.IsNullOrWhiteSpace(homeDrive) && !string.IsNullOrWhiteSpace(homePath))
        {
            homeDrive += "\\";
            if (homePath!.StartsWith('\\')
                || homePath.StartsWith('/'))
            {
                homePath = homeDrive + homePath;
            }

            if (Directory.Exists(home = Path.Combine(homeDrive, homePath!)))
                return Path.GetFullPath(home);
        }

        if (Environment.GetEnvironmentVariable("USERPROFILE") is var userProfile
            && !string.IsNullOrEmpty(userProfile) && Directory.Exists(userProfile))
        {
            return userProfile;
        }

        return null!;
    }
}
