using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git.Sets;

namespace AmpScm.Git;

public sealed class GitRemote : IGitNamedObject
{
    private GitRepository Repository { get; }

    private object? _rawUrl;

    internal GitRemote(GitRepository repository, string name, string? rawUrl)
    {
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _rawUrl = rawUrl;
    }

    public string Name { get; }
    public Uri? Url
    {
        get
        {
            if (_rawUrl is Uri uri)
                return uri;
            else if (RawUrl is string s && s.Length > 0 && Uri.TryCreate(s, UriKind.Absolute, out var parsed))
            {
                _rawUrl = parsed;
                return parsed;
            }
            else
                return null;
        }
    }

    internal string? RawUrl
    {
        get
        {
            if (_rawUrl == null)
                _rawUrl = Repository.Configuration.GetString("remote." + Name, "url") ?? "";

            return (_rawUrl is string s) ? s : _rawUrl.ToString();
        }
    }

    public async ValueTask ReadAsync()
    {
        if (_rawUrl == null)
            _rawUrl = await Repository.Configuration.GetStringAsync("remote." + Name, "url").ConfigureAwait(false) ?? "";
    }
}
