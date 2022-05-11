using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Git.Objects;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Objects;

namespace AmpScm.Git
{
    public sealed class GitTagObject : GitObject, IGitLazy<GitTagObject>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitTagObjectBucket? _rb;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        object? _obj;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string? _message;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string? _summary;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitSignature? _tagger;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        GitObjectType? _objType;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string? _name;

        internal GitTagObject(GitRepository repository, GitObjectBucket rdr, GitId id)
            : base(repository, id)
        {
            _rb = new GitTagObjectBucket(rdr);
        }

        public override GitObjectType Type => GitObjectType.Tag;

        public GitObject GitObject
        {
            get
            {
                if (_obj is GitObject ob)
                    return ob;

                Read();

                if (_obj is GitId oid)
                {
                    GitObject? t = Repository.ObjectRepository.GetByIdAsync<GitObject>(oid).AsTask().Result; // BAD async


                    if (t != null)
                    {
                        _obj = t;
                        return t;
                    }
                }

                return (_obj as GitObject)!;
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
            if (_rb is null)
                return;

            var (id, type) = await _rb.ReadObjectIdAsync().ConfigureAwait(false);

            _obj ??= id;
            _objType = type;

            _name = await _rb.ReadTagNameAsync().ConfigureAwait(false);

            _tagger = new GitSignature(await _rb.ReadTaggerAsync().ConfigureAwait(false));

            while (true)
            {
                var (bb, _) = await _rb.ReadUntilEolFullAsync(BucketEol.LF).ConfigureAwait(false);

                if (bb.IsEof)
                    break;

                _message += bb.ToUTF8String(); // Includes EOL
            }

            _rb.Dispose();
            _rb = null;
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
