using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Buckets.Git
{
    [CLSCompliant(false)]
    public struct GitCommitGenerationValue : IEquatable<GitCommitGenerationValue>
    {
        ulong generationV1Value;
        long timeCorrection;

        public GitCommitGenerationValue(int generation, DateTimeOffset timeStamp)
            : this(generation, timeStamp, long.MinValue)
        {
        }

        public GitCommitGenerationValue(int generation, DateTimeOffset timeStamp, long correctedTimeOffset)
        {
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation), generation, message: null);
            else if (generation >= 0x3FFFFFFF)
                generation = 0x3FFFFFFF;

            if (correctedTimeOffset < 0)
                correctedTimeOffset = long.MinValue;

            var s = timeStamp.ToUnixTimeSeconds();

            if (s < 0)
                throw new ArgumentOutOfRangeException(nameof(timeStamp), timeStamp, message: null);

            if (s >= 0x3FFFFFFFF)
                s = 0x3FFFFFFFF; // Overflow. We should use overflow handling over 34 bit...
                                 // So somewhere before 2038 + 4 * (2038-1970)... 2242...

            generationV1Value = ((ulong)generation << 34) | (ulong)s;
            timeCorrection = correctedTimeOffset;
        }

        public GitCommitGenerationValue(int generation, long timeValue)
            : this(generation, timeValue, long.MinValue)
        {

        }

        public GitCommitGenerationValue(int generation, long timeValue, long correctedTimeOffset)
        {
            if (generation < 0)
                throw new ArgumentOutOfRangeException(nameof(generation), generation, message: null);
            else if (generation >= 0x3FFFFFFF)
                generation = 0x3FFFFFFF;

            if (correctedTimeOffset < 0)
                correctedTimeOffset = long.MinValue;

            var s = timeValue;

            if (s < 0)
                throw new ArgumentOutOfRangeException(nameof(timeValue), timeValue, message: null);

            if (s >= 0x3FFFFFFFF)
                s = 0x3FFFFFFFF; // Overflow. We should use overflow handling over 34 bit...
                                 // So somewhere before 2038 + 4 * (2038-1970)... 2242...

            generationV1Value = ((ulong)generation << 2) | (((ulong)s & 0x300000000) >> 32) | (((ulong)s & 0xFFFFFFFF) << 32);
            timeCorrection = correctedTimeOffset;
        }

        public long CommitTimeValue => (long)(generationV1Value & 0x3FFFFFFFF);

        public DateTimeOffset CommitTime => DateTimeOffset.FromUnixTimeSeconds(CommitTimeValue);

        public int Generation => (int)(generationV1Value >> 34);


        public long CorrectedTimeValue => CommitTimeValue + (HasTimeCorrection ? (long)timeCorrection : 0);

        public DateTimeOffset CorrectedTime => CommitTime.AddSeconds((HasTimeCorrection ? (double)timeCorrection : 0.0));

        public bool HasTimeCorrection => timeCorrection != uint.MaxValue;


        public static GitCommitGenerationValue FromValue(ulong value)
        {
            return new GitCommitGenerationValue { generationV1Value = value };
        }

        public static GitCommitGenerationValue FromValue(ulong value, long offset)
        {
            return new GitCommitGenerationValue { generationV1Value = value, timeCorrection = offset };
        }

        public ulong Value => generationV1Value;

        public bool HasValue => generationV1Value != 0;

        public override bool Equals(object? obj)
        {
            return (obj is GitCommitGenerationValue other) && Equals(other);
        }

        public bool Equals(GitCommitGenerationValue other)
        {
            return other.Value == Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(GitCommitGenerationValue left, GitCommitGenerationValue right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GitCommitGenerationValue left, GitCommitGenerationValue right)
        {
            return !(left == right);
        }
    }
}
