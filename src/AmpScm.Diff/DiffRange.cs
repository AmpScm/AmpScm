using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AmpScm.Diff;

/// <summary>
/// Represents the range [Start..End)  (Including Start, but not including End)
/// </summary>
[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
[StructLayout(LayoutKind.Auto)]
public readonly record struct DiffRange
{
    public int Start { get; }
    public int Length => End - Start;
    public int End { get; }

    public bool IsEmpty => Length == 0;

    public DiffRange(int start, int end)
    {
        Start = start;
        End = end;
    }

    public static implicit operator Range(DiffRange cr)
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
            return $"[{Start},{End - 1}]";
        else if (Length == 1)
            return $"[{Start}]";
        else
            return $"({Start})";
    }
}
