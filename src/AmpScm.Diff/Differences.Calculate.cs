using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpScm.Diff
{
    public partial class Differences
    {
        public static Differences Calculate<TTokenizer, TToken>(TTokenizer tokenizer, IEnumerable<TToken> original, IEnumerable<TToken> modified)
            where TTokenizer : notnull, IEqualityComparer<TToken>
            where TToken : notnull
        {
            var orig = original.ToArray();
            var mod = modified.ToArray();

            if (orig.Length == 0 && mod.Length == 0)
                return new Differences(Array.Empty<DiffRange>());

            var origMod = new bool[orig.Length];
            var modMod = new bool[mod.Length];

            int MAX = orig.Length + mod.Length;
            // vector for the (0,0) to (x,y) search
            int[] downVector = new int[2 * MAX];
            // vector for the (u,v) to (N,M) search
            int[] upVector = new int[2 * MAX];

            LCS(tokenizer, orig, origMod,0, orig.Length, mod, modMod, 0, mod.Length, downVector, upVector);

            Optimize(tokenizer, orig, origMod);
            Optimize(tokenizer, mod, modMod);

            return new Differences(CreateDiffs(orig, origMod, mod, modMod));
        }

        private static IEnumerable<DiffRange> CreateDiffs<TToken>(TToken[] orig, bool[] origMod, TToken[] mod, bool[] modMod) where TToken : notnull
        {
            int StartA, StartB;
            int LineA, LineB;

            DiffRange last = new DiffRange();

            LineA = 0;
            LineB = 0;
            while (LineA < orig.Length || LineB < mod.Length)
            {
                if ((LineA < orig.Length) && !origMod[LineA]
                  && (LineB < mod.Length) && !modMod[LineB])
                {
                    // equal lines
                    LineA++;
                    LineB++;

                }
                else
                {
                    // maybe deleted and/or inserted lines
                    StartA = LineA;
                    StartB = LineB;

                    while (LineA < orig.Length && (LineB >= mod.Length || origMod[LineA]))
                        LineA++;

                    while (LineB < mod.Length && (LineA >= orig.Length || modMod[LineB]))
                        LineB++;

                    if ((StartA < LineA) || (StartB < LineB))
                    {
                        if (last.Original.End < StartA || last.Modified.End <StartB)
                        {
                            yield return new DiffRange
                            {
                                Type = HunkType.Same,
                                Original = new(last.Original.End + 1, StartA),
                                Modified = new(last.Modified.End + 1, StartB),
                            };
                        }
                        // store a new difference-item
                        yield return last = new DiffRange
                        {
                            Type = HunkType.Different,
                            Original = new(StartA, LineA),
                            Modified = new(StartB, LineB)
                        };
                    }
                }
            }

            if (last.Original.End < orig.Length || last.Modified.End < mod.Length)
            {
                yield return new DiffRange
                {
                    Type = HunkType.Same,
                    Original = new(last.Original.End + 1, orig.Length),
                    Modified = new(last.Modified.End + 1, mod.Length),
                };
            }
        }

        public static Differences Calculate<TTokenizer, TToken>(TTokenizer tokenizer, IEnumerable<TToken> original, IEnumerable<TToken> modified, IEnumerable<TToken> latest)
            where TTokenizer : notnull, IEqualityComparer<TToken>
            where TToken : notnull, IComparable<TToken>
        {
            var orig = original.ToArray();
            var mod = modified.ToArray();
            var last = latest.ToArray();
            int start;

            if (orig.Length == 0 && mod.Length == 0 && last.Length == 0)
                return new Differences(Enumerable.Empty<DiffRange>());

            start = Math.Min(orig.Length, mod.Length);

            // Drop common head
            for (int i = 0; i < orig.Length && i < mod.Length && i < last.Length; i++)
            {
                if (!tokenizer.Equals(orig[i], mod[i]) && !tokenizer.Equals(orig[i], last[i]))
                {
                    start = i;
                    break;
                }
            }

            int origLength = orig.Length - start;
            int modLength = mod.Length - start;
            int lastLength = last.Length - start;
            int commonTail = 0;

            if (origLength == 0 && modLength == 0 && lastLength == 0)
                return new Differences(new DiffRange[] { new() { Type = HunkType.Same, Original = new(0, start), Modified = new(0, start), Latest = new(0, start) } });

            // Drop common tail
            while (origLength > 0 && modLength > 0 && lastLength > 0)
            {
                if (tokenizer.Equals(orig[start + origLength - 1], mod[start + modLength - 1])
                    && tokenizer.Equals(orig[start + origLength - 1], last[start + lastLength - 1]))
                {
                    origLength--;
                    modLength--;
                    lastLength--;
                    commonTail++;
                }
                else
                    break;
            }

            // Ok now we have changes between orig[start..start+origLenth] and mod[start...start+modLength]
            List<DiffRange> diffs = new List<DiffRange>();

            if (start > 0)
                diffs.Add(new() { Type = HunkType.Same, Original = new(0, start), Modified = new(0, start), Latest = new(0, start) });

            // TODO: Perform reall diff!
            diffs.Add(new DiffRange { Type = HunkType.Different, Original = new(start, origLength+start), Modified = new(start, modLength+start), Latest = new(start, lastLength + start) });

            if (commonTail > 0)
                diffs.Add(new() { Type = HunkType.Same, Original = new(orig.Length - commonTail, orig.Length), Modified = new(mod.Length - commonTail, mod.Length), Latest = new(last.Length - commonTail, last.Length) });

            return new Differences(diffs);
        }
  
        /// <summary>
        /// If a sequence of modified lines starts with a line that contains the same content
        /// as the line that appends the changes, the difference sequence is modified so that the
        /// appended line and not the starting line is marked as modified.
        /// This leads to more readable diff sequences when comparing text files.
        /// </summary>
        /// <param name="cmp"></param>
        /// <param name="data">A Diff data buffer containing the identified changes.</param>
        /// <param name="mod"></param>
        private static void Optimize<TToken>(IEqualityComparer<TToken> cmp, TToken[] data, bool[] mod)
            where TToken : notnull
        {
            int StartPos, EndPos;

            StartPos = 0;
            while (StartPos < data.Length)
            {
                while ((StartPos < data.Length) && !mod[StartPos])
                    StartPos++;
                EndPos = StartPos;
                while ((EndPos < data.Length) && mod[EndPos])
                    EndPos++;

                if ((EndPos < data.Length) && cmp.Equals(data[StartPos], data[EndPos]))
                {
                    mod[StartPos] = false;
                    mod[EndPos] = true;
                }
                else
                {
                    StartPos = EndPos;
                }
            }
        }


        /// <summary>
        /// This is the algorithm to find the Shortest Middle Snake (SMS).
        /// </summary>
        /// <param name="cmp"></param>
        /// <param name="dataA">sequence A</param>
        /// <param name="lowerA">lower bound of the actual range in DataA</param>
        /// <param name="upperA">upper bound of the actual range in DataA (exclusive)</param>
        /// <param name="dataB">sequence B</param>
        /// <param name="lowerB">lower bound of the actual range in DataB</param>
        /// <param name="upperB">upper bound of the actual range in DataB (exclusive)</param>
        /// <param name="downVector">a vector for the (0,0) to (x,y) search. Passed as a parameter for speed reasons.</param>
        /// <param name="upVector">a vector for the (u,v) to (N,M) search. Passed as a parameter for speed reasons.</param>
        /// <returns>a MiddleSnakeData record containing x,y and u,v</returns>
        private static (int x, int y) SMS<TToken>(IEqualityComparer<TToken> cmp, TToken[] dataA, int lowerA, int upperA, TToken[] dataB, int lowerB, int upperB, int[] downVector, int[] upVector)
        {
            int MAX = dataA.Length + dataB.Length + 1;

            int DownK = lowerA - lowerB; // the k-line to start the forward search
            int UpK = upperA - upperB; // the k-line to start the reverse search

            int Delta = (upperA - lowerA) - (upperB - lowerB);
            bool oddDelta = (Delta & 1) != 0;

            // The vectors in the publication accepts negative indexes. the vectors implemented here are 0-based
            // and are access using a specific offset: UpOffset UpVector and DownOffset for DownVektor
            int DownOffset = MAX - DownK;
            int UpOffset = MAX - UpK;

            int MaxD = ((upperA - lowerA + upperB - lowerB) / 2) + 1;

            // init vectors
            downVector[DownOffset + DownK + 1] = lowerA;
            upVector[UpOffset + UpK - 1] = upperA;

            for (int D = 0; D <= MaxD; D++)
            {

                // Extend the forward path.
                for (int k = DownK - D; k <= DownK + D; k += 2)
                {
                    // find the only or better starting point
                    int x, y;
                    if (k == DownK - D)
                        x = downVector[DownOffset + k + 1]; // down
                    else
                    {
                        x = downVector[DownOffset + k - 1] + 1; // a step to the right
                        if ((k < DownK + D) && (downVector[DownOffset + k + 1] >= x))
                            x = downVector[DownOffset + k + 1]; // down
                    }
                    y = x - k;

                    // find the end of the furthest reaching forward D-path in diagonal k.
                    while ((x < upperA) && (y < upperB) && cmp.Equals(dataA[x],dataB[y]))
                    {
                        x++; y++;
                    }
                    downVector[DownOffset + k] = x;

                    // overlap ?
                    if (oddDelta && (UpK - D < k) && (k < UpK + D))
                    {
                        if (upVector[UpOffset + k] <= downVector[DownOffset + k])
                            return (downVector[DownOffset + k], downVector[DownOffset + k] - k);
                    }

                }

                // Extend the reverse path.
                for (int k = UpK - D; k <= UpK + D; k += 2)
                {
                    // Debug.Write(0, "SMS", "extend reverse path " + k.ToString());

                    // find the only or better starting point
                    int x, y;
                    if (k == UpK + D)
                        x = upVector[UpOffset + k - 1]; // up
                    else
                    {
                        x = upVector[UpOffset + k + 1] - 1; // left
                        if ((k > UpK - D) && (upVector[UpOffset + k - 1] < x))
                            x = upVector[UpOffset + k - 1]; // up
                    }
                    y = x - k;

                    while ((x > lowerA) && (y > lowerB) && cmp.Equals(dataA[x - 1], dataB[y - 1]))
                    {
                        x--; y--; // diagonal
                    }
                    upVector[UpOffset + k] = x;

                    // overlap ?
                    if (!oddDelta && (DownK - D <= k) && (k <= DownK + D))
                    {
                        if (upVector[UpOffset + k] <= downVector[DownOffset + k])
                        {
                            return (downVector[DownOffset + k], downVector[DownOffset + k] - k);
                        }
                    }

                }

            }

            throw new InvalidOperationException("the algorithm should never come here.");
        }


        /// <summary>
        /// This is the divide-and-conquer implementation of the longes common-subsequence (LCS) 
        /// algorithm.
        /// The published algorithm passes recursively parts of the A and B sequences.
        /// To avoid copying these arrays the lower and upper bounds are passed while the sequences stay constant.
        /// </summary>
        /// <param name="cmp"></param>
        /// <param name="dataA">sequence A</param>
        /// <param name="modA"></param>
        /// <param name="lowerA">lower bound of the actual range in DataA</param>
        /// <param name="upperA">upper bound of the actual range in DataA (exclusive)</param>
        /// <param name="dataB">sequence B</param>
        /// <param name="modB"></param>
        /// <param name="lowerB">lower bound of the actual range in DataB</param>
        /// <param name="upperB">upper bound of the actual range in DataB (exclusive)</param>
        /// <param name="downVector">a vector for the (0,0) to (x,y) search. Passed as a parameter for speed reasons.</param>
        /// <param name="upVector">a vector for the (u,v) to (N,M) search. Passed as a parameter for speed reasons.</param>
        static void LCS<TToken>(IEqualityComparer<TToken> cmp, TToken[] dataA, bool[] modA, int lowerA, int upperA, TToken[] dataB, bool[] modB, int lowerB, int upperB, int[] downVector, int[] upVector)
            where TToken :notnull
        {
            // Fast walkthrough equal lines at the start
            while (lowerA < upperA && lowerB < upperB && cmp.Equals(dataA[lowerA], dataB[lowerB]))
            {
                lowerA++; lowerB++;
            }

            // Fast walkthrough equal lines at the end
            while (lowerA < upperA && lowerB < upperB && cmp.Equals(dataA[upperA - 1], dataB[upperB - 1]))
            {
                --upperA; --upperB;
            }

            if (lowerA == upperA)
            {
                // mark as inserted lines.
                while (lowerB < upperB)
                    modB[lowerB++] = true;

            }
            else if (lowerB == upperB)
            {
                // mark as deleted lines.
                while (lowerA < upperA)
                    modA[lowerA++] = true;

            }
            else
            {
                // Find the middle snakea and length of an optimal path for A and B
                var smsrd = SMS(cmp, dataA, lowerA, upperA, dataB, lowerB, upperB, downVector, upVector);

                // The path is from LowerX to (x,y) and (x,y) to UpperX
                LCS(cmp, dataA, modA, lowerA, smsrd.x, dataB, modB, lowerB, smsrd.y, downVector, upVector);
                LCS(cmp, dataA, modA, smsrd.x, upperA, dataB, modB, smsrd.y, upperB, downVector, upVector);
            }
        } 
    }
}
