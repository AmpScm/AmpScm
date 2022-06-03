using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: CLSCompliant(true)]

namespace AmpScm.Diff
{
    public enum HunkType
    {
        Same,
        Different,
        Conflict,        
    }

    public sealed partial class Differences : IReadOnlyCollection<DiffRange>
    {
        Differences(IEnumerable<DiffRange> range)
        {
            Ranges = range;
        }

        public bool HasConflicts => Ranges.Any(x => x.Type == HunkType.Conflict);
        public bool HasChanges => Ranges.Any(x=>x.Type != HunkType.Same);

        public float Similarity
        {
            get
            {
                int same = 0;
                int maxDifferent = 0;

                //TODO: Tune to be more similar to the value calculated by git
                foreach (var h in Ranges)
                {
                    if (h.Type == HunkType.Same)
                        same += h.Original.Length;
                    else
                        maxDifferent += Math.Max(h.Original.Length, h.Modified.Length);
                }

                if (maxDifferent == 0)
                    return 1.0f;

                return (float)same / (float)(same + maxDifferent);
            }
        }

        IEnumerable<DiffRange> Ranges { get; }

        int IReadOnlyCollection<DiffRange>.Count => Ranges.Count();

        IEnumerator<DiffRange> IEnumerable<DiffRange>.GetEnumerator()
        {
            return Ranges.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Ranges.GetEnumerator();
        }
    }
}
