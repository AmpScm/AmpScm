﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Git.Repository.Implementation;

namespace AmpScm.Git.Repository
{
    public class GitConfiguration
    {
        protected GitRepository Repository { get; }
        string _gitDir;
        bool _loaded;
        int _repositoryFormatVersion;
        readonly Dictionary<(string, string?, string), string> _config = new Dictionary<(string, string?, string), string>();

        static readonly Lazy<string> _gitExePath = new Lazy<string>(GetGitExePath, true);
        static readonly Lazy<string> _homeDir = new Lazy<string>(GetHomeDirectory, true);
        public static string GitProgramPath => _gitExePath.Value;
        public static string UserHomeDir => _homeDir.Value;

        public GitConfiguration(GitRepository gitRepository, string gitDir)
        {
            Repository = gitRepository;
            _gitDir = gitDir;
            _lazy = new Lazy<GitLazyConfig>(() => new GitLazyConfig(this));
        }

        internal async ValueTask LoadAsync()
        {
            if (_loaded) return;

            foreach (string path in GetGitConfigurationFilePaths())
            {
                await LoadConfig(path);
            }

            await LoadConfig(Path.Combine(_gitDir, "config"));
        }

        async ValueTask LoadConfig(string path)
        {
            using var b = FileBucket.OpenRead(path);
            using var cr = new GitConfigurationReaderBucket(b);

            while (await cr.ReadConfigItem() is GitConfigurationItem item)
            {
                if (item.Group == "core" || item.Group == "extension")
                    ParseCore(item);
                else if (item.Group == "includeif")
                    await ParseIncludeIf(path, item);

                _config[(item.Group, item.SubGroup, item.Key)] = item.Value ?? "\xFF";
            }
            _loaded = true;
        }

        private async ValueTask ParseIncludeIf(string path, GitConfigurationItem item)
        {
            if (!(item.SubGroup is var check) || string.IsNullOrEmpty(check))
                return;
            else if (item.Key != "path" || item.Value == null)
                return; // No other types documented yet

            bool caseInsensitive = false;
            if (check.StartsWith("gitdir:"))
            { }
            else if (check.StartsWith("gitdir/i:"))
            {
                caseInsensitive = true;
                check = check.Remove(6, 2);
            }

            string dir = ApplyHomeDir(check.Substring(7).Trim());

            if (GitGlob.Match(dir, Repository.FullPath, GitGlobFlags.ParentPath | (caseInsensitive ? GitGlobFlags.CaseInsensitive : GitGlobFlags.None)))
            {
                string newPath = Path.Combine(Path.GetDirectoryName(path)!, ApplyHomeDir(item.Value!));

                if (!string.IsNullOrEmpty(newPath) && File.Exists(newPath))
                {
                    await LoadConfig(Path.GetFullPath(newPath));
                }
            }
        }

        static string ApplyHomeDir(string path)
        {
            if (path != null && path.StartsWith("~") && UserHomeDir is var homeDir && homeDir != null)
            {
                if (path.StartsWith("~/"))
                    path = homeDir!.TrimEnd(Path.DirectorySeparatorChar) + path.Substring(1);
                else if (char.IsLetterOrDigit(path, 1))
                    path = Path.GetDirectoryName(homeDir) + path.Substring(1); // Might need more work on linux, but not common
            }
            return path;
        }

        private void ParseCore(GitConfigurationItem item)
        {
            if (item.Key == "repositoryformatversion" && item.Group == "core")
            {
                if (int.TryParse(item.Value, out var version))
                    _repositoryFormatVersion = version;
            }
        }

        internal IEnumerable<(string, string)> GetGroup(string group, string? subGroup)
        {
            if (!_loaded)
                LoadAsync().GetAwaiter().GetResult();

            group = group.ToLowerInvariant();

            foreach (var v in _config)
            {
                var (g, s, k) = v.Key;

                if (group == g && subGroup == s)
                    yield return (k, v.Value);
            }
        }

        public IEnumerable<string> GetSubGroups(string group)
        {
            if (!_loaded)
                LoadAsync().GetAwaiter().GetResult();

            group = group.ToLowerInvariant();
            HashSet<string> subGroups = new HashSet<string>();

            foreach (var v in _config)
            {
                var (g, s, k) = v.Key;

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

        public async ValueTask<int?> GetIntAsync(string group, string key)
        {
            await LoadAsync();

            if (_config.TryGetValue((group, null, key), out var vResult)
                && int.TryParse(vResult, out var r))
            {
                return r;
            }
            else
                return null;
        }

        internal int? GetInt(string group, string key)
        {
            return GetIntAsync(group, key).Result;
        }

        public async ValueTask<string?> GetStringAsync(string group, string key)
        {
            await LoadAsync();

            if (_config.TryGetValue((group, null, key), out var vResult))
            {
                if (vResult == "\xFF")
                    return "";
                return vResult;
            }
            else
                return null;
        }

        internal string? GetString(string group, string key)
        {
            return GetStringAsync(group, key).Result;
        }

        public ValueTask<bool> GetBoolAsync(string group, string key, bool defaultValue)
        {
            return GetBoolAsync(group, subGroup: null, key, defaultValue);
        }

        public async ValueTask<bool> GetBoolAsync(string group, string subGroup, string key, bool defaultValue)
        {
            await LoadAsync();

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
                else if (vResult != null && vResult.Length == 0)
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

                return defaultValue;
            }
            else
                return defaultValue;
        }

        public bool GetBool(string group, string key, bool defaultValue)
        {
            return GetBoolAsync(group, key, defaultValue).Result;
        }

        public bool GetBool(string group, string subGroup, string key, bool defaultValue)
        {
            return GetBoolAsync(group, subGroup, key, defaultValue).Result;
        }

        internal class GitLazyConfig
        {
            GitConfiguration Configuration { get; }
            Lazy<bool> _repositoryIsLazy;

            public GitLazyConfig(GitConfiguration config)
            {
                Configuration = config ?? throw new ArgumentNullException(nameof(config));

                _repositoryIsLazy = new Lazy<bool>(GetRepositoryIsLazy);
            }

            bool GetRepositoryIsLazy()
            {
                if (Configuration._loaded && Configuration._repositoryFormatVersion == 0)
                    return false;

                foreach (var v in Configuration.GetSubGroups("remote"))
                {
                    if (Configuration.GetBool("remote", v, "promisor", false))
                        return true;
                }

                return false;
            }

            public bool RepositoryIsLazy => _repositoryIsLazy.Value;

        }

        Lazy<GitLazyConfig> _lazy;

        internal GitLazyConfig Lazy => _lazy.Value;

        static string GetGitExePath()
        {
            return GitExePathLook() ?? GetExePathWhere() ?? null!;
        }

        private static string? GetExePathWhere()
        {
            try
            {
                var psi = new ProcessStartInfo(Environment.NewLine == "\n" ? "which" : "where", "git");
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;

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
                    string git = outputText.Split(new[] { '\n' }, 2)[0].Trim();

                    if (File.Exists(git)
                        && File.Exists(git = Path.GetFullPath(git)))
                    {
                        return git;
                    }
                }

                return null;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
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
                    catch { }
                }
                return null;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
        }

        public GitSignature Identity
        {
            get
            {
                var username = GetString("user", "name") ?? "John Doe";
                var email = GetString("user", "email") ?? "john@john.doe.local";

                return new GitSignature(username, email, DateTime.Now);
            }
        }

        public static IEnumerable<string> GetGitConfigurationFilePaths(bool includeSystem = true)
        {
            string f;
            if (includeSystem && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GIT_CONFIG_NOSYSTEM")))
            {
                if (GitProgramPath != null)
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

            if (UserHomeDir is string home && File.Exists(f = Path.Combine(home, ".gitconfig")))
                yield return Path.GetFullPath(f);
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

        static string GetHomeDirectory()
        {
            if (Environment.GetEnvironmentVariable("HOME") is string home
                && Directory.Exists(home))
            {
                return Path.GetFullPath(home);
            }
            else if (Environment.GetEnvironmentVariable("HOMEDRIVE") is var homeDrive
                && Environment.GetEnvironmentVariable("HOMEPATH") is var homePath)
            {
                homeDrive += "\\";
                if (homePath.StartsWith("\\") || homePath.StartsWith("/"))
                    homePath = homeDrive + homePath;

                if (Directory.Exists(home = Path.Combine(homeDrive, homePath!)))
                    return Path.GetFullPath(home);
            }
            else if (Environment.GetEnvironmentVariable("USERPROFILE") is var userProfile)
            {
                if (Directory.Exists(userProfile))
                    return userProfile;
            }

            return null!;
        }
    }
}