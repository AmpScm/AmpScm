using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Git;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets.Git
{
    [DebuggerDisplay("{ToString(),nq}")]
    public sealed record GitConfigurationItem : IComparable<GitConfigurationItem>
    {
        public string Group { get; set; } = "";
        public string? SubGroup { get; set; }
        public string Key { get; set; } = "";
        public string? Value { get; set; } = "";

        public int CompareTo(GitConfigurationItem? other)
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

        public static bool operator <(GitConfigurationItem left, GitConfigurationItem right)
        {
            return (left is null) ? !(right is null) : left.CompareTo(right) < 0;
        }

        public static bool operator <=(GitConfigurationItem left, GitConfigurationItem right)
        {
            return (left is null) || left.CompareTo(right) <= 0;
        }

        public static bool operator >(GitConfigurationItem left, GitConfigurationItem right)
        {
            return !(left is null) && left.CompareTo(right) > 0;
        }

        public static bool operator >=(GitConfigurationItem left, GitConfigurationItem right)
        {
            return (left is null) ? (right is null) : left.CompareTo(right) >= 0;
        }
    }
    public class GitConfigurationReaderBucket : GitBucket
    {
        string? _group;
        string? _subGroup;
        BucketEolState? _state;

        public GitConfigurationReaderBucket(Bucket inner) : base(inner)
        {
        }

        public async ValueTask<GitConfigurationItem?> ReadConfigItem()
        {
            while (true)
            {
                var (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.AnyEol, _state ??= new BucketEolState()).ConfigureAwait(false);

                if (bb.IsEof)
                    return null;

                string line = bb.ToUTF8String(eol).Trim();

                if (line.Length == 0)
                    continue;

                while (line.EndsWith("\\", StringComparison.Ordinal))
                {
                    line = line.Substring(0, line.Length - 1);

                    (bb, eol) = await Inner.ReadUntilEolFullAsync(BucketEol.AnyEol, _state).ConfigureAwait(false);

                    if (bb.IsEmpty)
                        break;

                    line += bb.ToUTF8String(eol).TrimEnd();
                }

                if (line[0] == '#' || line[0] == ';')
                    continue;

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

                        while(i < line.Length)
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
                    _group = line.Substring(groupStart, groupEnd - groupStart).ToUpperInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

                    if (subGroupEnd > 0)
                        _subGroup = line.Substring(subGroupStart, subGroupEnd - subGroupStart);
                }
                else if (_group is not null)
                {
                    int i = 0;
                    string? value;
                    while(i < line.Length && char.IsLetterOrDigit(line, i) || line[i] == '-')
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
                        return new GitConfigurationItem { Group = _group, SubGroup = _subGroup!, Key = line.Substring(0, keyEnd), Value = value };

                }
            }
        }

        public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
        {
            throw new NotImplementedException();
        }
    }
}
