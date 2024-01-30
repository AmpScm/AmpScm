using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Git;

namespace AmpScm.Buckets.Git;

public record GitReferenceLogRecord
{
    public GitId Original { get; init; } = default!;
    public GitId Target { get; init; } = default!;
    public GitSignatureRecord Signature { get; init; } = default!;
    public string? Reason { get; init; } = default!;


    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();

        sb.Append(Original);
        sb.Append(' ');
        sb.Append(Target);
        sb.Append(' ');
        sb.Append(Signature);
        sb.Append('\t');
        sb.Append(Reason);
        sb.Append('\n');

        return sb.ToString();
    }
}

public record GitSignatureRecord
{
    public string Name { get; init; } = default!;
    public string Email { get; init; } = default!;
    public DateTimeOffset When { get; init; }

    public override string ToString()
    {
        string offsetMinutes;

        if (When.Offset == TimeSpan.Zero)
            offsetMinutes = "+0000";
        else
        {
            int mins = (int)When.Offset.TotalMinutes;

            int hours = mins / 60;

            offsetMinutes = (mins + (hours * 100) - (hours * 60)).ToString("0000", CultureInfo.InvariantCulture);

            if (offsetMinutes.Length != 5)
                offsetMinutes = "+" + offsetMinutes;
        }

        return $"{Name} <{Email}> {When.ToUnixTimeSeconds()} {offsetMinutes}";
    }

    public static bool TryReadFromBucket(BucketBytes bucketBytes, [NotNullWhen(true)] out GitSignatureRecord? record)
    {
        int n = bucketBytes.IndexOf('<');
        if (n < 0)
        {
            record = null!;
            return false;
        }
        int n2 = bucketBytes.IndexOf('>', n);
        if (n2 < 0)
        {
            record = null!;
            return false;
        }
        record = new GitSignatureRecord
        {
            Name = bucketBytes.ToUTF8String(0, n - 1),
            Email = bucketBytes.ToUTF8String(n + 1, n2 - n - 1),
            When = ParseWhen(bucketBytes.Slice(n2 + 2))
        };
        return true;
    }

    private static DateTimeOffset ParseWhen(BucketBytes value)
    {
        var time = value.Split((byte)' ', 2);
        if (long.TryParse(time[0]
#if NET8_0_OR_GREATER
            .Span
#else
            .ToUTF8String()
#endif
            , NumberStyles.None, CultureInfo.InvariantCulture, out var unixtime)
            && int.TryParse(time[1]
#if NET8_0_OR_GREATER
            .Span
#else
            .ToUTF8String()
#endif
            , NumberStyles.None | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var offset))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixtime).ToOffset(TimeSpan.FromMinutes((offset / 100) * 60 + (offset % 100)));
        }
        return DateTimeOffset.Now;
    }
}

public class GitReferenceLogBucket : GitBucket
{
    private GitId? _lastId;
    private int? _idLength;

    public GitReferenceLogBucket(Bucket source) : base(source)
    {
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        while (await (ReadGitReferenceLogRecordAsync().ConfigureAwait(false)) != null)
        {

        }
        return BucketBytes.Eof;
    }

    public async ValueTask<GitReferenceLogRecord?> ReadGitReferenceLogRecordAsync()
    {
        var (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.LF, eolState: null).ConfigureAwait(false);

        if (bb.IsEof)
            return null;

        int prefix = bb.IndexOf('\t', (2 * _idLength + 2) ?? 0);

        if (!_idLength.HasValue)
        {
            _idLength = bb.IndexOf(' ');

            if (prefix < 0 || _idLength < GitId.HashLength(GitIdType.Sha1) * 2 || _idLength * 2 + 2 > prefix)
                throw new GitBucketException($"Unable to determine reference log format in {Name} Bucket");
        }

        return new GitReferenceLogRecord
        {
            Original = ReadGitId(bb, 0) ?? throw new GitBucketException($"Bad {nameof(GitReferenceLogRecord.Original)} OID in RefLog line from {Source.Name}"),
            Target = ReadGitId(bb, _idLength.Value + 1) ?? throw new GitBucketException($"Bad {nameof(GitReferenceLogRecord.Target)} OID in RefLog line from {Source.Name}"),
            Signature = GitSignatureRecord.TryReadFromBucket(bb.Slice(0, prefix).Slice(2 * (_idLength.Value + 1)), out var v) ? v : throw new GitBucketException("Invalid reference log item"),
            Reason = bb.Slice(prefix + 1).ToUTF8String(eol)
        };
    }

    private GitId? ReadGitId(BucketBytes bb, int offset)
    {
        var s = bb.Slice(offset, _idLength!.Value);

        if (GitId.TryParse(s, out var oid))
        {
            oid = (_lastId == oid) ? _lastId : oid;
            _lastId = oid;
            return oid;
        }
        else
            return null;
    }
}
