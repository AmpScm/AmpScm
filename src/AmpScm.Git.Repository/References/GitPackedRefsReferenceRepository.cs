using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AmpScm.Buckets;

namespace AmpScm.Git.References;

internal class GitPackedRefsReferenceRepository : GitReferenceRepository
{
    public const string PackedRefsFile = "packed-refs";
    public GitPackedRefsReferenceRepository(GitReferenceRepository repository, string gitDir, string workTreeDir)
        : base(repository.Repository, gitDir, workTreeDir)
    {
    }

    [DebuggerDisplay($"{nameof(GitRefPeel.Id)} {nameof(GitRefPeel.Name)} {nameof(GitRefPeel.Peeled)}")]
    private protected sealed class GitRefPeel
    {
        public string Name { get; set; } = null!;
        public GitId Id { get; set; } = null!;
        public GitId? Peeled { get; set; }
    }

    private Dictionary<string, GitRefPeel>? _peelRefs;

    private async ValueTask Read()
    {
        if (_peelRefs != null)
            return;

        _peelRefs = new Dictionary<string, GitRefPeel>(StringComparer.Ordinal);

        await ReadRefs().ConfigureAwait(false);
    }

    private protected virtual async ValueTask ReadRefs()
    {
        string fileName = Path.Combine(GitDir, PackedRefsFile);

        if (!File.Exists(fileName))
            return;

        try
        {
            using var sr = FileBucket.OpenRead(fileName, forAsync: false);

            var idLength = GitId.HashLength(Repository.InternalConfig.IdType) * 2;

            GitRefPeel? last = null;
            while (true)
            {
                var (bb, eol) = await sr.ReadExactlyUntilEolAsync(BucketEol.LF).ConfigureAwait(false);

                if (bb.IsEof)
                    return;

                bb = bb.Trim(eol);
                ParseLineToPeel(bb, ref last, idLength);
            }
        }
        catch (FileNotFoundException)
        {
            return;
        }
    }

    protected internal override async ValueTask<GitReference?> ResolveAsync(GitReference gitReference)
    {
        await Read().ConfigureAwait(false);

        if (_peelRefs!.TryGetValue(gitReference.Name, out var v))
        {
            return new GitReference(this, v.Name, v.Id).SetPeeled(v.Peeled);
        }

        return null;
    }

    public override async ValueTask<IEnumerable<GitReference>> ResolveByOidAsync(GitId id, HashSet<string> processed)
    {
        await Read().ConfigureAwait(false);

        return Walk();

        // Private function to generate result as IEnumerable
        IEnumerable<GitReference> Walk()
        {
            foreach (var v in _peelRefs!.Values)
            {
                // TODO: Do we know if the packed reference is overridden by a local file?
                if (!processed.Contains(v.Name) && (v.Id == id || v.Peeled == id))
                    yield return new GitReference(this, v.Name, v.Id).SetPeeled(v.Peeled);
            }
        }
    }

    private protected void ParseLineToPeel(BucketBytes line, ref GitRefPeel? last, int idLength)
    {
        if (char.IsLetterOrDigit((char)line[0]) && line.Length > idLength + 1)
        {
            if (GitId.TryParse(line.Slice(0, idLength), out var oid))
            {
                string name = line.Slice(idLength + 1).Trim().ToUTF8String();

                if (GitReference.ValidName(name, allowSpecialSymbols: false))
                    _peelRefs![name] = last = new GitRefPeel { Name = name, Id = oid };
                else if (name.EndsWith("^{}", StringComparison.OrdinalIgnoreCase)
                    && last != null && name.Substring(0, name.Length - 3) == last.Name)
                {
                    last.Peeled = oid;
                }
            }
        }
        else if (line[0] == '^')
        {
            if (last != null && GitId.TryParse(line.Slice(1).TrimEnd(), out var oid))
            {
                last.Peeled = oid;
            }
            last = null;
        }
    }

    private protected void ParseLineToPeel(string line, ref GitRefPeel? last, int idLength)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (char.IsLetterOrDigit(line, 0) && line.Length > idLength + 1)
        {
            if (GitId.TryParse(line.Substring(0, idLength), out var oid))
            {
                string name = line.Substring(idLength + 1).Trim();

                if (GitReference.ValidName(name, allowSpecialSymbols: false))
                    _peelRefs![name] = last = new GitRefPeel { Name = name, Id = oid };
                else if (name.EndsWith("^{}", StringComparison.OrdinalIgnoreCase)
                    && last != null && name.Substring(0, name.Length - 3) == last.Name)
                {
                    last.Peeled = oid;
                }
            }
        }
        else if (line[0] == '^')
        {
            if (last != null && GitId.TryParse(line.Substring(1).TrimEnd(), out var oid))
            {
                last.Peeled = oid;
            }
            last = null;
        }
    }

    public override async IAsyncEnumerable<GitReference> GetAll(HashSet<string> alreadyReturned)
    {
        await Read().ConfigureAwait(false);

        foreach (var v in _peelRefs!.Values)
        {
            yield return new GitReference(this, v.Name, v.Id).SetPeeled(v.Peeled);
        }
    }

    protected internal override async ValueTask<GitReference?> GetUnsafeAsync(string name)
    {
        await Read().ConfigureAwait(false);

        if (_peelRefs!.TryGetValue(name, out var v))
            return new GitReference(this, v.Name, v.Id).SetPeeled(v.Peeled);

        return null;
    }
}
