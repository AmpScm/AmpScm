
namespace AmpScm.Diff
{
    public sealed record class DiffChunk
    {
        public DifferenceType Type { get; init; }
        public DiffRange Original { get; init; }
        public DiffRange Modified { get; init; }
        public DiffRange? Latest { get; init; }
    }
}
