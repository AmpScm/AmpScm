
namespace AmpScm.Diff
{
    public sealed record class DiffRange
    {
        public HunkType Type { get; init; }
        public DiffTokenRange Original { get; init; }
        public DiffTokenRange Modified { get; init; }
        public DiffTokenRange? Latest { get; init; }
    }
}
