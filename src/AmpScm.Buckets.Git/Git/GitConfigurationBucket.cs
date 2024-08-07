﻿using System.Diagnostics;
using System.Text;

namespace AmpScm.Buckets.Git;

[DebuggerDisplay("{ToString(),nq}")]
public sealed record GitConfigurationRecord : IComparable<GitConfigurationRecord>, IEquatable<GitConfigurationRecord>
{
    /// <summary>
    /// Group of configuration item. Normalized to UPPER-CASE using <see cref="String.ToLowerInvariant"/>.
    /// </summary>
    /// <remarks>The git internals normalize to lower case, but CA1308 and common sense dictate that if w
    /// we normalize we should normalize to upper case to avoid localization issues.</remarks>
    public string Group { get; set; } = "";

    /// <summary>
    /// Sub-Group (case sensitive).
    /// </summary>
    public string? SubGroup { get; set; }

    /// <summary>
    /// Key of configuration item (case insensitive). Normalized to lower-case using <see cref="String.ToLowerInvariant"/>.
    /// </summary>
    public string Key { get; set; } = "";

    public string? Value { get; set; } = "";

    public int CompareTo(GitConfigurationRecord? other)
    {
        int n = string.CompareOrdinal(Group, other?.Group);
        if (n == 0)
            n = string.CompareOrdinal(SubGroup, other?.SubGroup);
        if (n == 0)
            n = string.CompareOrdinal(Key, other?.Key);
        if (n == 0)
            n = string.CompareOrdinal(Value, other?.Value);

        return n;
    }

    public override string ToString()
    {
        if (SubGroup != null)
            return $"{Group}.{SubGroup}.{Key}: {Value ?? "<<empty>>"}";
        else
            return $"{Group}.{Key}: {Value ?? "<<empty>>"}";
    }

    public static bool operator <(GitConfigurationRecord left, GitConfigurationRecord right)
    {
        return (left is null) ? !(right is null) : left.CompareTo(right) < 0;
    }

    public static bool operator <=(GitConfigurationRecord left, GitConfigurationRecord right)
    {
        return (left is null) || left.CompareTo(right) <= 0;
    }

    public static bool operator >(GitConfigurationRecord left, GitConfigurationRecord right)
    {
        return !(left is null) && left.CompareTo(right) > 0;
    }

    public static bool operator >=(GitConfigurationRecord left, GitConfigurationRecord right)
    {
        return (left is null) ? (right is null) : left.CompareTo(right) >= 0;
    }
}
public class GitConfigurationBucket : GitBucket
{
    private string? _group;
    private string? _subGroup;
    private readonly BucketEolState _state = new();

    public GitConfigurationBucket(Bucket source) : base(source)
    {
    }

    public async ValueTask<GitConfigurationRecord?> ReadRecord()
    {
        while (true)
        {
            var (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.LF, eolState: _state).ConfigureAwait(false);

            if (bb.IsEof)
                return null;

            bb = bb.Trim(eol);

            if (bb.Length == 0)
                continue;

            if (bb[0] == '#' || bb[0] == ';')
                continue;

            string line = bb.ToUTF8String();

            while (line.EndsWith('\\'))
            {
                line = line.Substring(0, line.Length - 1);

                (bb, eol) = await Source.ReadExactlyUntilEolAsync(BucketEol.LF, eolState: _state).ConfigureAwait(false);

                if (bb.IsEmpty)
                    break;

                line += bb.TrimEnd(eol).ToUTF8String();
            }

            if (line[0] == '[')
            {
                _group = _subGroup = null;
                int i = 1;

                while (i < line.Length && char.IsWhiteSpace(line, i))
                    i++;

                int groupStart = i;

                while (i < line.Length && char.IsLetterOrDigit(line, i))
                    i++;

                int groupEnd = i;

                while (i < line.Length && char.IsWhiteSpace(line, i))
                    i++;

                int subGroupStart = -1;
                int subGroupEnd = -1;
                if (i < line.Length && line[i] == '\"')
                {
                    i++;
                    subGroupStart = i;

                    while (i < line.Length)
                    {
                        if (line[i] == '\\' && line.Length + 1 < line.Length)
                        {
                            i += 2;
                            continue;
                        }
                        else if (line[i] == '\"')
                            break;
                        else
                            i++;
                    }

                    if (i < line.Length && line[i] == '\"')
                    {
                        subGroupEnd = i++;
                    }

                    while (i < line.Length && char.IsWhiteSpace(line, i))
                        i++;
                }

                if (i < line.Length && line[i] == ']')
                {
                    i++;
                    while (i < line.Length && char.IsWhiteSpace(line, i))
                        i++;

                    // Skip comment at end of line ?
                    if (i < line.Length && line[i] != '#' && line[i] != ';')
                        continue; // Not a proper header line
                }
                else
                    continue;

#pragma warning disable CA1308 // Normalize strings to uppercase
                _group = line.Substring(groupStart, groupEnd - groupStart).ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

                if (subGroupEnd > 0)
                    _subGroup = line.Substring(subGroupStart, subGroupEnd - subGroupStart);
            }
            else if (_group is not null)
            {
                int i = 0;
                string? value;
                while (i < line.Length && char.IsLetterOrDigit(line, i) || line[i] == '-')
                    i++;

                int keyEnd = i;

                while (i < line.Length && char.IsWhiteSpace(line, i))
                    i++;

                if (i < line.Length && line[i] == '=')
                {
                    i++;
                    while (i < line.Length && char.IsWhiteSpace(line, i))
                        i++;

                    value = line.Substring(i);
                }
                // Skip comment at end of line ?
                else if (i < line.Length && line[i] != '#' && line[i] != ';')
                    continue; // Not a proper value line
                else
                    value = null;

                if (keyEnd > 0)
#pragma warning disable CA1308 // Normalize strings to uppercase
                    return new GitConfigurationRecord { Group = _group, SubGroup = _subGroup!, Key = line.Substring(0, keyEnd).ToLowerInvariant(), Value = Unescape(value) };
#pragma warning restore CA1308 // Normalize strings to uppercase

            }
        }
    }

    private static string? Unescape(string? value)
    {
        if (value is null)
            return null;

        if (value.Contains('\\', StringComparison.Ordinal))
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\\')
                {
                    i++;
                    if (i < value.Length)
                    {
                        switch (value[i])
                        {
                            case 'n':
                                sb.Append('\n');
                                break;
                            case 'b':
                                sb.Append('\b');
                                break;
                            case 't':
                                sb.Append('\t');
                                break;
                            case 'r':
                                sb.Append('\r');
                                break;
                            default:
                                sb.Append(value[i]);
                                break;
                        }
                    }
                }
                else
                    sb.Append(value[i]);
            }
            return sb.ToString();
        }
        else
            return value;
    }

    public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
    {
        while (await ReadRecord().ConfigureAwait(false) is not null)
        {

        }
        return BucketBytes.Eof;
    }
}
