using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Git.Objects;
using AmpScm.Buckets.Cryptography;
using AmpScm.Buckets.Specialized;
using AmpScm.Git.Objects;

namespace AmpScm.Git;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
public sealed class GitTagObject : GitObject, IGitLazy<GitTagObject>
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private GitTagObjectBucket? _rb;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private object? _obj;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string? _message;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string? _summary;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private GitSignature? _tagger;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private GitObjectType? _objType;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string? _name;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _signed;

    internal GitTagObject(GitRepository repository, Bucket rdr, GitId id)
        : base(repository, id)
    {
        _rb = new GitTagObjectBucket(rdr);
    }

    public override GitObjectType Type => GitObjectType.Tag;

    public GitId GitObjectId
    {
        get
        {
            if (_obj is null)
                Read();

            return (_obj as GitObject)?.Id ?? (GitId)_obj!;
        }
    }

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


    public string Message
    {
        get
        {
            if (_message is null)
                Read();

            return _message!;
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

    public string Summary
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

    /// <summary>
    /// Gets a boolean indicating whether this tag is signed by the creator.
    /// </summary>
    public bool IsSigned
    {
        get => Message is not null && _signed;
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
            var (bb, eol) = await _rb.ReadExactlyUntilEolAsync(BucketEol.LF).ConfigureAwait(false);

            if (bb.IsEof)
                break;

            _message += bb.ToUTF8String(); // Includes EOL
        }

        if (!(await _rb.ReadSignatureBytesAsync().ConfigureAwait(false)).IsEmpty)
            _signed = true;

        _rb.Dispose();
        _rb = null;
    }

    ValueTask<GitId> IGitLazy<GitTagObject>.WriteToAsync(GitRepository repository)
    {
        if (repository != Repository && !repository.Blobs.ContainsId(Id))
            return this.AsWriter().WriteToAsync(repository);
        else
            return new(Id);
    }

    public async ValueTask<bool> VerifySignatureAsync(Func<ReadOnlyMemory<byte>, ValueTask<GitPublicKey?>>? findKey = null)
    {
        bool succeeded = false;
        GitObjectBucket b = (await Repository.ObjectRepository.FetchGitIdBucketAsync(Id).ConfigureAwait(false))!;
        var src = GitTagObjectBucket.ForSignature(b);

        using (var gob = new GitTagObjectBucket(b.Duplicate(), HandleSubBucket))
        {
            await gob.ReadUntilEofAsync().ConfigureAwait(false);
        }

        return succeeded;

        async ValueTask HandleSubBucket(GitSubBucketType arg1, Bucket bucket)
        {
            if (arg1 == GitSubBucketType.Signature || arg1 == GitSubBucketType.SignatureSha256)
            {
                var rdx = new Radix64ArmorBucket(bucket);
                using var gpg = new SignatureBucket(rdx);

                var fingerprint = await gpg.ReadFingerprintAsync().ConfigureAwait(false);

                GitPublicKey? key = null;

                if (findKey != null)
                    key = await findKey(fingerprint).ConfigureAwait(false);

                if (key is null)
                    key = await Repository.PublicKeyRepository.GetKeyAsync(fingerprint).ConfigureAwait(false);

                if (key is not null)
                {
                    if(await gpg.VerifyAsync(src, key).ConfigureAwait(false))
                        succeeded = true;
                }
                else
                    await bucket.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
            }
            else
                await bucket.ReadUntilEofAndCloseAsync().ConfigureAwait(false);
        }
    }
}
