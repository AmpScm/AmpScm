using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Git.Objects
{
    public static partial class GitObjectWriterExtensions
    {
        /// <summary>
        /// Imports git objects from the 'fast-import' source <paramref name="source"/>
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="BucketException"></exception>
        /// <remarks>Imports objects and updates references based on information in <paramref name="source"/></remarks>
        public static async ValueTask FastImportAsync(this GitRepository repository, Bucket source)
        {
            if (repository is null)
                throw new ArgumentNullException(nameof(repository));
            else if (source is null)
                throw new ArgumentNullException(nameof(source));

            using (source)
            {
                var marks = new Dictionary<string, GitId>();
                var refs = new Dictionary<string, GitId>();

                while (true)
                {
                    BucketBytes bb;
                    BucketEol eol;
                    string type;
                    string follow;
                    while (true)
                    {
                        (bb, eol) = await source.ReadUntilEolFullAsync(BucketEol.LF).ConfigureAwait(false);

                        if (bb.IsEof)
                            return;

                        var parts = bb.Split((byte)' ', 2);

                        if (parts.Length == 2)
                        {
                            type = parts[0].ToASCIIString();
                            follow = parts[1].ToUTF8String(eol);
                        }
                        else
                        {
                            type = bb.ToUTF8String(eol);
                            follow = "";
                        }

                        if (type.Length == 0)
                            break;
                        if (type == "reset")
                            continue;
                        else if (type == "from")
                        {
                            (bb, eol) = await source.ReadUntilEolAsync(BucketEol.LF).ConfigureAwait(false);
                            continue;
                        }
                        else
                            break;
                    }

                    if (type.Length == 0)
                        break;

                    string? mark;
                    if (type != "tag")
                    {
                        (bb, eol) = await source.ReadUntilEolAsync(BucketEol.LF).ConfigureAwait(false);

                        if (!bb.StartsWithASCII("mark "))
                            throw new BucketException($"Missing mark: {bb.ToUTF8String()}");
                        mark = bb.Slice(5).ToASCIIString(eol);
                    }
                    else
                        mark = null;

                    List<string>? headers = new List<string>();

                    while (true)
                    {
                        (bb, eol) = await source.ReadUntilEolAsync(BucketEol.LF).ConfigureAwait(false);

                        if (bb.StartsWithASCII("data "))
                        {
                            break;
                        }
                        else
                        {
                            var n = bb.IndexOf((byte)' ');
                            if (n < 0)
                                throw new BucketException($"Bad header {bb.ToUTF8String(eol)}");

                            headers.Add(bb.ToUTF8String(eol));
                        }
                    }

                    long len = Convert.ToInt64(bb.Slice(5).ToASCIIString(eol), 10);

                    {
                        Bucket body = source.TakeExact(len).NoClose();
                        GitId? id = null;

                        switch (type)
                        {
                            case "blob":
                                {
                                    GitBlobWriter bw = GitBlobWriter.CreateFrom(body);

                                    id = await bw.WriteToAsync(repository).ConfigureAwait(false);

                                    (bb, eol) = await source.ReadUntilEolFullAsync(BucketEol.LF).ConfigureAwait(false);

                                    if (bb.IsEof)
                                        return;

                                    break;
                                }
                            case "commit":
                                using (body)
                                {
                                    string message = (await body.ReadFullAsync((int)len).ConfigureAwait(false)).ToUTF8String();

                                    var cw = await FillCommit(message, source, marks, repository).ConfigureAwait(false);

                                    string author = headers.Single(x => x.StartsWith("author ", StringComparison.Ordinal)).Substring(7);
                                    string committer = headers.Single(x => x.StartsWith("committer ", StringComparison.Ordinal)).Substring(10);

                                    cw.Message = message;
                                    if (Buckets.Git.GitSignatureRecord.TryReadFromBucket(Encoding.UTF8.GetBytes(author), out var authSig))
                                        cw.Author = new GitSignature(authSig);
                                    if (Buckets.Git.GitSignatureRecord.TryReadFromBucket(Encoding.UTF8.GetBytes(committer), out var commitSig))
                                        cw.Committer = new GitSignature(commitSig);

                                    id = await cw.WriteToAsync(repository).ConfigureAwait(false);

                                    if (!string.IsNullOrEmpty(follow) && id is not null)
                                    {
                                        refs[follow] = id;
                                    }
                                    break;
                                }
                            case "tag":
                                using (body)
                                {
                                    string message = (await body.ReadFullAsync((int)len).ConfigureAwait(false)).ToUTF8String();

                                    string from = headers.Single(x => x.StartsWith("from ", StringComparison.Ordinal)).Substring(5);
                                    string tagger = headers.Single(x => x.StartsWith("tagger ", StringComparison.Ordinal)).Substring(7);

                                    var c = repository.Commits[marks[from]] ?? throw new GitRepositoryException("Failed to load commit");

                                    GitTagObjectWriter tw = GitTagObjectWriter.Create(c, follow);
                                    tw.Message = message;

                                    if (Buckets.Git.GitSignatureRecord.TryReadFromBucket(Encoding.UTF8.GetBytes(tagger), out var tagSig))
                                        tw.Tagger = new GitSignature(tagSig);

                                    id = await tw.WriteToAsync(repository).ConfigureAwait(false);
                                    refs[$"refs/tags/{follow}"] = id;
                                    break;
                                }
                            default:
                                body.Dispose();
                                throw new BucketException($"Unexpected object type {type}");
                        }

                        await body.ReadSkipAsync(long.MaxValue).ConfigureAwait(false);

                        if (mark is not null && id is not null)
                            marks.Add(mark, id);
                    }

                }

                if (refs.Count > 0)
                {
                    using (var t = repository.References.CreateUpdateTransaction())
                    {
                        foreach (var k in refs)
                            t.Update(k.Key, k.Value);

                        await t.CommitAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        private static async ValueTask<GitCommitWriter> FillCommit(string message, Bucket source, Dictionary<string, GitId> marks, GitRepository repo)
        {
            List<GitCommit>? parents = null;

            GitCommitWriter gcw = GitCommitWriter.Create();
            gcw.Message = message;
            GitTree? tree = null;
            while (true)
            {
                var (bb, eol) = await source.ReadUntilEolFullAsync(BucketEol.LF).ConfigureAwait(false);

                if (bb.IsEof)
                    return gcw;

                bb = bb.Trim(eol);
                if (bb.IsEmpty)
                    break;

                if (bb.StartsWithASCII("from "))
                {
                    string mark = bb.Slice(5).ToUTF8String();

                    var c = repo.Commits[marks[mark]]!;
                    parents ??= new();
                    parents.Insert(0, c);
                    tree = c.Tree;
                    gcw.Tree = c.Tree.AsWriter();
                }
                else if (bb.StartsWithASCII("merge "))
                {
                    string mark = bb.Slice(6).ToUTF8String();

                    parents ??= new();
                    parents.Add(repo.Commits[marks[mark]]!);
                }
                else if (bb.StartsWithASCII("M "))
                {
                    var items = bb.Slice(2).ToUTF8String().Split(new[] { ' ' }, 3);

                    GitTreeElementType? fileType = (GitTreeElementType)Convert.ToInt32(items[0], 8);
                    string mark = items[1];
                    string name = items[2];
                    var b = repo.Blobs[marks[mark]]!;

#if NET6_0_OR_GREATER
                    if (!Enum.IsDefined<GitTreeElementType>(fileType.Value))
                        fileType = null;
#else
                    if (!Enum.IsDefined(typeof(GitTreeElementType),fileType))
                        fileType = null;
#endif

                    if (tree?.AllFiles.TryGet(name, out _) ?? false)
                    {
                        gcw.Tree.Replace(name, b, fileType);
                    }
                    else
                        gcw.Tree.Add(name, b, fileType);

                }
                else if (bb.StartsWithASCII("D "))
                {
                    string name = bb.Slice(2).ToUTF8String();
                    gcw.Tree.Remove(name);
                }
            }

            gcw.Parents = parents!;
            return gcw;
        }
    }
}
