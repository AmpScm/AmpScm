using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;
using AmpScm.Git;

namespace AmpScm.Buckets.Git
{
    public class GitBundleBucket : GitBucket
    {
        int? _version;
        BState _state;
        byte? _prefetched;
        GitIdType _idType;
        bool _ret;
        enum BState
        {
            Capabilities,
            Prerequisites,
            References,
            Body
        }


        public GitBundleBucket(Bucket inner) : base(inner)
        {
        }

        public async ValueTask<int> ReadVersionAsync()
        {
            if (_version.HasValue)
                return _version.Value;

            var (bb, eol) = await Inner.ReadExactlyUntilEolAsync(BucketEol.LF, 32);

            if (eol == BucketEol.LF && bb.EqualsASCII("# v2 git bundle\n"))
                _version = 2;
            else if (eol == BucketEol.LF && bb.EqualsASCII("# v3 git bundle\n"))
                _version = 3;
            else
                throw new GitBucketException($"Unexpected bundle header in {Name} bucket");

            return _version.Value;
        }

        public async ValueTask<(Bucket Bucket, GitIdType IdType)> ReadPackBucketAsync()
        {
            while (_state != BState.Body)
            {
                (var id, _) = await ReadReferenceAsync();

                if (id is null)
                    break;
            }

            if (_state != BState.Body)
                throw new BucketException();

            if (!_ret)
            {
                _ret = true;
                NoDispose();

                return (Inner, _idType);
            }
            else
                throw new BucketException();
        }


        public override async ValueTask<BucketBytes> ReadAsync(int requested = 2146435071)
        {
            if (!_ret)
            {
                var (bucket, _) = await ReadPackBucketAsync();

                await bucket.ReadUntilEofAndCloseAsync();
            }
            
            return BucketBytes.Eof;
        }

        public async ValueTask<(string? Key, string? Value)> ReadCapabilityAsync()
        {
            if (!_version.HasValue)
                await ReadVersionAsync();

            if (_version < 3 || _state != BState.Capabilities)
            {
                if (_state == BState.Capabilities)
                    _state = BState.Prerequisites;

                return (null, null);
            }

            var bb = await Inner.PollAsync();

            if (bb.Length > 0 && bb[0] != '@')
            {
                _state = BState.Prerequisites;
                return (null, null);
            }

            BucketEol eol;
            if (bb.IsEmpty && !bb.IsEof)
            {
                bb = await Inner.ReadAsync(1);

                if (bb.Length == 1 && bb[0] != '@')
                {
                    if (bb[0] == '\n')
                        _state = BState.Body;
                    else
                    {
                        _state = BState.Prerequisites;
                        _prefetched = bb[0];
                    }
                    return (null, null);
                }
                else if (bb.IsEmpty)
                {
                    _state = BState.Body;
                    return (null, null);
                }

                (bb, eol) = await Inner.ReadExactlyUntilEolAsync(BucketEol.LF);
            }
            else
            {
                (bb, eol) = await Inner.ReadExactlyUntilEolAsync(BucketEol.LF);

                if (!bb.IsEmpty)
                    bb = bb.Slice(1); // Skip the '@'
            }

            if (bb.IsEmpty)
                throw new GitBucketException($"Bad capability in '{Name}' bucket");

            var n = bb.IndexOf('=');

            if (n >= 0)
            {
                var key = bb.Slice(0, n).ToUTF8String();
                var value = bb.Slice(n + 1, eol).ToUTF8String();

                if (key == "object-format")
                    _idType = (value == "sha256") ? GitIdType.Sha256 : GitIdType.Sha1;

                return (key, value);
            }
            else
                return (bb.Slice(n + 1, eol).ToUTF8String(), null);
        }

        public async ValueTask<(GitId? Id, string? Comment)> ReadPrerequisiteAsync()
        {
            if (!_version.HasValue)
                await ReadVersionAsync();

            while (_state < BState.Prerequisites)
            {
                var (key, _) = await ReadCapabilityAsync();

                if (key is null && _state < BState.Prerequisites)
                    return (null, null); // EOF
            }

            if (_idType == GitIdType.None)
                _idType = GitIdType.Sha1;

            if (_state > BState.Prerequisites)
                return (null, null);

            BucketBytes bb;
            if (_prefetched.HasValue && _prefetched != '-')
            {
                _state = BState.References;
                return (null, null);
            }
            
            BucketEol eol;

            if (!_prefetched.HasValue)
            {
                bb = await Inner.PollAsync(1);

                if (!bb.IsEmpty && bb[0] != '-')
                {
                    _state = BState.References;
                    return (null, null);
                }
                else if (bb.IsEmpty)
                {
                    bb = await Inner.ReadAsync(1);

                    if (bb.IsEmpty)
                    {
                        _state = BState.References;
                        return (null, null);
                    }
                    else if (bb[0] != '-')
                    {
                        _prefetched = bb[0];
                        _state = BState.References;
                        return (null, null);
                    }
                }
            }

            _prefetched = null;
            (bb, eol) = await Inner.ReadExactlyUntilEolAsync(BucketEol.LF);

            var oidLen = _idType.HashLength() * 2;

            if (bb.Length > oidLen)
            {
                var oidBb = bb.Slice(0, oidLen + 1);

                if (!GitId.TryParse(oidBb, out var id))
                    throw new GitBucketException($"Bad prerequisite '-{bb.TrimEnd(eol).ToASCIIString()}' in '{Name}' bucket");

                return (id, oidLen > oidLen+1 ?  bb.Slice(oidLen + 1, eol).ToUTF8String() : null);
            }
            else
            {
                _state = BState.Body;
                return (null, null);
            }
        }

        public async ValueTask<(GitId? Id, string? Name)> ReadReferenceAsync()
        {
            if (!_version.HasValue)
                await ReadVersionAsync();

            while (_state < BState.References)
            {
                (var id, _) = await ReadPrerequisiteAsync();

                if (id is null && _state < BState.References)
                    return (null, null); // EOF
            }

            if (_state > BState.References)
                return (null, null);

            Bucket src = Inner;
            if (_prefetched.HasValue)
                src = new[] { _prefetched.Value }.AsBucket() + Inner;

            var (line, eol) = await src.ReadExactlyUntilEolAsync(BucketEol.LF);

            _prefetched = null;

            var oidLen = _idType.HashLength() * 2;
            if (line.Length > oidLen)
            {                
                var oidBb = line.Slice(0, oidLen + 1);

                if (!GitId.TryParse(oidBb, out var id))
                    throw new GitBucketException($"Bad reference '{line.TrimEnd(eol).ToASCIIString()}' in '{Name}' bucket");

                return (id, line.Slice(oidLen + 1, eol).ToUTF8String());
            }
            else
            {
                if (line.Length == 1)
                    _state = BState.Body;

                return (null, null);
            }
        }
    }
}
