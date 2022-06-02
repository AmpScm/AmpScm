using System;
using System.Diagnostics;

namespace AmpScm.Diff
{
    [DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
    public readonly record struct DiffTokenRange
    {
        public int Start { get; }
        public int Length { get; }
        public int End => Start + Length-1;

        public DiffTokenRange(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public static implicit operator Range(DiffTokenRange cr)
        {
            return cr.ToRange();
        }

        public Range ToRange()
        {
            return new Range(Start, Start + Length);
        }

        public override string ToString()
        {
            if (Length > 1)
                return $"{Start}..{End}";
            else if (Length == 1)
                return $"{Start}";
            else
                return "-";
        }
    }
}
