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
    public enum DifferenceType
    {
        None,
        Modified,
        Conflict,        
    }

    public sealed partial class DifferenceSet : IReadOnlyCollection<DiffChunk>
    {
        DifferenceSet(IEnumerable<DiffChunk> range)
        {
            Ranges = range;
        }

        public bool HasConflicts => Ranges.Any(x => x.Type == DifferenceType.Conflict);
        public bool HasChanges => Ranges.Any(x=>x.Type != DifferenceType.None);

        public float Similarity
        {
            get
            {
                int same = 0;
                int maxDifferent = 0;

                //TODO: Tune to be more similar to the value calculated by git
                foreach (var h in Ranges)
                {
                    if (h.Type == DifferenceType.None)
                        same += h.Original.Length;
                    else
                        maxDifferent += Math.Max(h.Original.Length, h.Modified.Length);
                }

                if (maxDifferent == 0)
                    return 1.0f;

                return (float)same / (float)(same + maxDifferent);
            }
        }

        IEnumerable<DiffChunk> Ranges { get; }

        int IReadOnlyCollection<DiffChunk>.Count => Ranges.Count();

        IEnumerator<DiffChunk> IEnumerable<DiffChunk>.GetEnumerator()
        {
            return Ranges.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Ranges.GetEnumerator();
        }
    }
}
