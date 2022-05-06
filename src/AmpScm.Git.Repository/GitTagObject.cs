using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Objects;

namespace AmpScm.Git
{
    public sealed class GitTagObject : GitObject, IGitLazy<GitTagObject>
    {
        private object _obj;
        string? _message, _summary;
        GitSignature? _tagger;
        Dictionary<string, string>? _headers;
        GitObjectType? _objType;
        string? _name;

        internal GitTagObject(GitRepository repository, GitBucket rdr, GitId id)
            : base(repository, id)
        {
            _obj = rdr;
        }

        public override GitObjectType Type => GitObjectType.Tag;

        public GitObject GitObject
        {
            get
            {
                if (_obj is GitObject ob)
                    return ob;

                Read();

                if (_obj is string s && !string.IsNullOrEmpty(s) && GitId.TryParse(s, out var oid))
                {
                    _obj = oid;

                    try
                    {
                        GitObject? t = Repository.ObjectRepository.GetByIdAsync<GitObject>(oid).AsTask().Result; // BAD async

                        if (t != null)
                        {
                            _obj = t;
                            return t;
                        }
                    }
                    catch
                    {
                        _obj = s; // Continue later
                        throw;
                    }
                }

                return null!;
            }
        }


        public string? Message
        {
            get
            {
                if (_message is null)
                    Read();

                return _message;
            }
        }

        public string? Name
        {
            get
            {
                if (_name is null)
                    Read();

                return _name;
            }
        }

        public string? Summary
        {
            get
            {
                return _summary ?? (_summary = GitTools.CreateSummary(Message));
            }
        }

        public GitSignature Tagger
        {
            get
            {
                if (_tagger is null)
                    Read();

                return _tagger!;
            }
        }

        public GitObjectType ObjectType
        {
            get
            {
                if (!_objType.HasValue)
                    Read();

                return _objType ?? GitObjectType.None;
            }
        }

        private void Read()
        {
            ReadAsync().AsTask().Wait();
        }

        public override async ValueTask ReadAsync()
        {
            if (_obj is Bucket b)
            {
                _obj = "";
                _objType = GitObjectType.None;
                BucketEolState? _eolState = null;

                while(true)
                {
                    var (bb, eol) = await b.ReadUntilEolFullAsync(BucketEol.LF, _eolState ??= new BucketEolState()).ConfigureAwait(false);

                    if (bb.IsEof || bb.Length == eol.CharCount())
                        break;

                    string line = bb.ToUTF8String(eol);

                    if (line.Length == 0)
                        break;

                    var parts = line.Split(new[] { ' ' }, 2);
                    switch (parts[0])
                    {
                        case "object":
                            _obj = parts[1];
                            break;
                        case "type":
                            if (parts[1] == "commit")
                                _objType = GitObjectType.Commit;
                            else if (parts[1] == "tree")
                                _objType = GitObjectType.Tree;
                            else if (parts[1] == "blob")
                                _objType = GitObjectType.Blob;
                            else if (parts[1] == "tag")
                                _objType = GitObjectType.Tag;
                            else
                                _objType = GitObjectType.None;
                            break;
                        case "tag":
                            _name = parts[1];
                            break;

                        case "tagger":
                            _tagger = new GitSignature(parts[1]);
                            break;

                        default:
                            if (!char.IsWhiteSpace(line, 0))
                            {
                                _headers ??= new Dictionary<string, string>();
                                if (_headers.TryGetValue(parts[0], out var v))
                                    _headers[parts[0]] = v + "\n" + parts[1];
                                else
                                    _headers[parts[0]] = parts[1];
                            }
                            break;
                    }
                }

                while(true)
                {
                    var (bb, _) = await b.ReadUntilEolFullAsync(BucketEol.Zero, _eolState ??= new BucketEolState()).ConfigureAwait(false);

                    if (bb.IsEof)
                        break;

                    _message += bb.ToUTF8String();
                }
            }
        }

        ValueTask<GitId> IGitLazy<GitTagObject>.WriteToAsync(GitRepository repository)
        {
            if (repository != Repository && !repository.Blobs.ContainsId(Id))
                return this.AsWriter().WriteToAsync(repository);
            else
                return new ValueTask<GitId>(Id);
        }
    }
}
