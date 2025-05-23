﻿using System.Diagnostics;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Git;

[DebuggerDisplay("{GitType}, Version={Version}, ObjectCount={ObjectCount}")]
public class GitPackHeaderBucket : GitBucket
{
    private GitPackHeader? _header;

    public GitPackHeaderBucket(Bucket source) : base(source)
    {
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        if (!_header.HasValue)
        {
            var bb = await Source.ReadAtLeastAsync(12).ConfigureAwait(false);

            GitPackHeader gph = new GitPackHeader
            {
                GitType = bb.ToASCIIString(0, 4),
                Version = NetBitConverter.ToInt32(bb, 4),
                ObjectCount = NetBitConverter.ToUInt32(bb, 8),
            };

            _header = gph;
        }

        return BucketBytes.Eof;
    }

    public string? GitType => _header.HasValue ? _header.Value.GitType : null;
    public int? Version => _header.HasValue ? _header.Value.Version : null;
    public long? ObjectCount => _header.HasValue ? _header.Value.ObjectCount : null;

    private struct GitPackHeader
    {
        public string GitType;
        public int Version;
        public uint ObjectCount;
    }
}
