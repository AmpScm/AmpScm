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
            int nNext = 0;
            Dictionary<string, int> map = new();

            var orig = original.ToArray();
            var mod = modified.ToArray();

            if (orig.Length == 0 && mod.Length == 0)
                return new Differences(Enumerable.Empty<DiffRange>());


            var origMod = new bool[orig.Length];
            var modMod = new bool[mod.Length];

            int MAX = orig.Length + mod.Length + 1;
            // vector for the (0,0) to (x,y) search
            int[] downVector = new int[2 * MAX + 2];
            // vector for the (u,v) to (N,M) search
            int[] upVector = new int[2 * MAX + 2];


            DiffData left = new DiffData(orig.Select(x => HashString((string)(object)x)).ToArray());
            DiffData right = new DiffData(mod.Select(x => HashString((string)(object)x)).ToArray());


            //Debug.Assert(left.data.Distinct().Count() == left.data.Length);
            //Debug.Assert(right.data.Distinct().Count() == right.data.Length);

            LCS(left,0, left.Length, right, 0, right.Length, downVector, upVector);

            Optimize(left);
            Optimize(right);

            return new Differences(CreateDiffs(left.data, left.modified, right.data, right.modified));

            int HashString(string v)
            {

                if (map.TryGetValue(v, out int r))
                    return r;

                map[v] = ++nNext;
                return nNext;
            }
#if false
            int start;

            start = Math.Min(orig.Length, mod.Length);

            // Drop common head
            for (int i = 0; i < orig.Length && i < mod.Length; i++)
            {
                if (!tokenizer.Equals(orig[i], mod[i]))
                {
                    start = i;
                    break;
                }
            }

            int origLength = orig.Length - start;
            int modLength = mod.Length - start;
            int commonTail = 0;

            if (origLength == 0 && modLength == 0)
                return new Differences(new DiffRange[] { new() { Type = HunkType.Same, Original = new(0, start), Modified = new(0, start) } });

            // Drop common tail
            while (origLength > 0 && modLength > 0)
            {
                if (tokenizer.Equals(orig[start + origLength - 1], mod[start + modLength - 1]))
                {
                    origLength--;
                    modLength--;
                    commonTail++;
                }
                else
                    break;
            }

            // Ok now we have changes between orig[start..start+origLenth] and mod[start...start+modLength]
            List<DiffRange> diffs = new List<DiffRange>();

            if (start > 0)
                diffs.Add(new() { Type = HunkType.Same, Original = new(0, start), Modified = new(0, start) });

            // TODO: Perform reall diff!
            diffs.Add(new DiffRange { Type = HunkType.Different, Original = new(start, origLength), Modified = new(start, modLength) });

            if (commonTail > 0)
                diffs.Add(new() { Type = HunkType.Same, Original = new(orig.Length - commonTail, commonTail), Modified = new(mod.Length - commonTail, commonTail) });

            return new Differences(diffs);
#endif
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
                        // while (LineA < DataA.Length && DataA.modified[LineA])
                        LineA++;

                    while (LineB < mod.Length && (LineA >= orig.Length || modMod[LineB]))
                        // while (LineB < DataB.Length && DataB.modified[LineB])
                        LineB++;

                    if ((StartA < LineA) || (StartB < LineB))
                    {
                        if (last.Original.End < StartA || last.Modified.End <StartB)
                        {
                            yield return new DiffRange
                            {
                                Type = HunkType.Same,
                                Original = new(last.Original.End + 1, StartA - last.Original.End - 1),
                                Modified = new(last.Modified.End + 1, StartA - last.Modified.End - 1),
                            };
                        }
                        // store a new difference-item
                        yield return last = new DiffRange
                        {
                            Type = HunkType.Different,
                            Original = new(StartA, LineA - StartA),
                            Modified = new(StartB, LineB - StartB)
                        };
                    } // if
                } // if
            } // while

            if (last.Original.End < orig.Length || last.Modified.End < mod.Length)
            {
                yield return new DiffRange
                {
                    Type = HunkType.Same,
                    Original = new(last.Original.End + 1, orig.Length - last.Original.End - 1),
                    Modified = new(last.Modified.End + 1, mod.Length - last.Modified.End - 1),
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
            diffs.Add(new DiffRange { Type = HunkType.Different, Original = new(start, origLength), Modified = new(start, modLength), Latest = new(start, lastLength) });

            if (commonTail > 0)
                diffs.Add(new() { Type = HunkType.Same, Original = new(orig.Length - commonTail, commonTail), Modified = new(mod.Length - commonTail, commonTail), Latest = new(last.Length - commonTail, commonTail) });

            return new Differences(diffs);
        }
  
        /// <summary>
        /// If a sequence of modified lines starts with a line that contains the same content
        /// as the line that appends the changes, the difference sequence is modified so that the
        /// appended line and not the starting line is marked as modified.
        /// This leads to more readable diff sequences when comparing text files.
        /// </summary>
        /// <param name="Data">A Diff data buffer containing the identified changes.</param>
        private static void Optimize(DiffData Data)
        {
            int StartPos, EndPos;

            StartPos = 0;
            while (StartPos < Data.Length)
            {
                while ((StartPos < Data.Length) && (Data.modified[StartPos] == false))
                    StartPos++;
                EndPos = StartPos;
                while ((EndPos < Data.Length) && (Data.modified[EndPos] == true))
                    EndPos++;

                if ((EndPos < Data.Length) && (Data.data[StartPos] == Data.data[EndPos]))
                {
                    Data.modified[StartPos] = false;
                    Data.modified[EndPos] = true;
                }
                else
                {
                    StartPos = EndPos;
                } // if
            } // while
        } // Optimize


        /// <summary>
        /// Find the difference in 2 arrays of integers.
        /// </summary>
        /// <param name="ArrayA">A-version of the numbers (usualy the old one)</param>
        /// <param name="ArrayB">B-version of the numbers (usualy the new one)</param>
        /// <returns>Returns a array of Items that describe the differences.</returns>
        public static Item[] DiffInt(int[] ArrayA, int[] ArrayB)
        {
            // The A-Version of the data (original data) to be compared.
            DiffData DataA = new DiffData(ArrayA);

            // The B-Version of the data (modified data) to be compared.
            DiffData DataB = new DiffData(ArrayB);

            int MAX = DataA.Length + DataB.Length + 1;
            /// vector for the (0,0) to (x,y) search
            int[] DownVector = new int[2 * MAX + 2];
            /// vector for the (u,v) to (N,M) search
            int[] UpVector = new int[2 * MAX + 2];

            LCS(DataA, 0, DataA.Length, DataB, 0, DataB.Length, DownVector, UpVector);
            return CreateDiffs(DataA, DataB);
        } // Diff   


        /// <summary>
        /// This is the algorithm to find the Shortest Middle Snake (SMS).
        /// </summary>
        /// <param name="DataA">sequence A</param>
        /// <param name="LowerA">lower bound of the actual range in DataA</param>
        /// <param name="UpperA">upper bound of the actual range in DataA (exclusive)</param>
        /// <param name="DataB">sequence B</param>
        /// <param name="LowerB">lower bound of the actual range in DataB</param>
        /// <param name="UpperB">upper bound of the actual range in DataB (exclusive)</param>
        /// <param name="DownVector">a vector for the (0,0) to (x,y) search. Passed as a parameter for speed reasons.</param>
        /// <param name="UpVector">a vector for the (u,v) to (N,M) search. Passed as a parameter for speed reasons.</param>
        /// <returns>a MiddleSnakeData record containing x,y and u,v</returns>
        private static (int x, int y) SMS(DiffData DataA, int LowerA, int UpperA, DiffData DataB, int LowerB, int UpperB,
          int[] DownVector, int[] UpVector)
        {

            int MAX = DataA.Length + DataB.Length + 1;

            int DownK = LowerA - LowerB; // the k-line to start the forward search
            int UpK = UpperA - UpperB; // the k-line to start the reverse search

            int Delta = (UpperA - LowerA) - (UpperB - LowerB);
            bool oddDelta = (Delta & 1) != 0;

            // The vectors in the publication accepts negative indexes. the vectors implemented here are 0-based
            // and are access using a specific offset: UpOffset UpVector and DownOffset for DownVektor
            int DownOffset = MAX - DownK;
            int UpOffset = MAX - UpK;

            int MaxD = ((UpperA - LowerA + UpperB - LowerB) / 2) + 1;

            // Debug.Write(2, "SMS", String.Format("Search the box: A[{0}-{1}] to B[{2}-{3}]", LowerA, UpperA, LowerB, UpperB));

            // init vectors
            DownVector[DownOffset + DownK + 1] = LowerA;
            UpVector[UpOffset + UpK - 1] = UpperA;

            for (int D = 0; D <= MaxD; D++)
            {

                // Extend the forward path.
                for (int k = DownK - D; k <= DownK + D; k += 2)
                {
                    // Debug.Write(0, "SMS", "extend forward path " + k.ToString());

                    // find the only or better starting point
                    int x, y;
                    if (k == DownK - D)
                    {
                        x = DownVector[DownOffset + k + 1]; // down
                    }
                    else
                    {
                        x = DownVector[DownOffset + k - 1] + 1; // a step to the right
                        if ((k < DownK + D) && (DownVector[DownOffset + k + 1] >= x))
                            x = DownVector[DownOffset + k + 1]; // down
                    }
                    y = x - k;

                    // find the end of the furthest reaching forward D-path in diagonal k.
                    while ((x < UpperA) && (y < UpperB) && (DataA.data[x] == DataB.data[y]))
                    {
                        x++; y++;
                    }
                    DownVector[DownOffset + k] = x;

                    // overlap ?
                    if (oddDelta && (UpK - D < k) && (k < UpK + D))
                    {
                        if (UpVector[UpOffset + k] <= DownVector[DownOffset + k])
                        {
                            return (DownVector[DownOffset + k], DownVector[DownOffset + k] - k);
                        } // if
                    } // if

                } // for k

                // Extend the reverse path.
                for (int k = UpK - D; k <= UpK + D; k += 2)
                {
                    // Debug.Write(0, "SMS", "extend reverse path " + k.ToString());

                    // find the only or better starting point
                    int x, y;
                    if (k == UpK + D)
                    {
                        x = UpVector[UpOffset + k - 1]; // up
                    }
                    else
                    {
                        x = UpVector[UpOffset + k + 1] - 1; // left
                        if ((k > UpK - D) && (UpVector[UpOffset + k - 1] < x))
                            x = UpVector[UpOffset + k - 1]; // up
                    } // if
                    y = x - k;

                    while ((x > LowerA) && (y > LowerB) && (DataA.data[x - 1] == DataB.data[y - 1]))
                    {
                        x--; y--; // diagonal
                    }
                    UpVector[UpOffset + k] = x;

                    // overlap ?
                    if (!oddDelta && (DownK - D <= k) && (k <= DownK + D))
                    {
                        if (UpVector[UpOffset + k] <= DownVector[DownOffset + k])
                        {
                            return (DownVector[DownOffset + k], DownVector[DownOffset + k] - k);
                        } // if
                    } // if

                } // for k

            } // for D

            throw new InvalidOperationException("the algorithm should never come here.");
        } // SMS


        /// <summary>
        /// This is the divide-and-conquer implementation of the longes common-subsequence (LCS) 
        /// algorithm.
        /// The published algorithm passes recursively parts of the A and B sequences.
        /// To avoid copying these arrays the lower and upper bounds are passed while the sequences stay constant.
        /// </summary>
        /// <param name="DataA">sequence A</param>
        /// <param name="LowerA">lower bound of the actual range in DataA</param>
        /// <param name="UpperA">upper bound of the actual range in DataA (exclusive)</param>
        /// <param name="DataB">sequence B</param>
        /// <param name="LowerB">lower bound of the actual range in DataB</param>
        /// <param name="UpperB">upper bound of the actual range in DataB (exclusive)</param>
        /// <param name="DownVector">a vector for the (0,0) to (x,y) search. Passed as a parameter for speed reasons.</param>
        /// <param name="UpVector">a vector for the (u,v) to (N,M) search. Passed as a parameter for speed reasons.</param>
        private static void LCS(DiffData DataA, int LowerA, int UpperA, DiffData DataB, int LowerB, int UpperB, int[] DownVector, int[] UpVector)
        {
            // Debug.Write(2, "LCS", String.Format("Analyse the box: A[{0}-{1}] to B[{2}-{3}]", LowerA, UpperA, LowerB, UpperB));

            // Fast walkthrough equal lines at the start
            while (LowerA < UpperA && LowerB < UpperB && DataA.data[LowerA] == DataB.data[LowerB])
            {
                LowerA++; LowerB++;
            }

            // Fast walkthrough equal lines at the end
            while (LowerA < UpperA && LowerB < UpperB && DataA.data[UpperA - 1] == DataB.data[UpperB - 1])
            {
                --UpperA; --UpperB;
            }

            if (LowerA == UpperA)
            {
                // mark as inserted lines.
                while (LowerB < UpperB)
                    DataB.modified[LowerB++] = true;

            }
            else if (LowerB == UpperB)
            {
                // mark as deleted lines.
                while (LowerA < UpperA)
                    DataA.modified[LowerA++] = true;

            }
            else
            {
                // Find the middle snakea and length of an optimal path for A and B
                var smsrd = SMS(DataA, LowerA, UpperA, DataB, LowerB, UpperB, DownVector, UpVector);
                // Debug.Write(2, "MiddleSnakeData", String.Format("{0},{1}", smsrd.x, smsrd.y));

                // The path is from LowerX to (x,y) and (x,y) to UpperX
                LCS(DataA, LowerA, smsrd.x, DataB, LowerB, smsrd.y, DownVector, UpVector);
                LCS(DataA, smsrd.x, UpperA, DataB, smsrd.y, UpperB, DownVector, UpVector);  // 2002.09.20: no need for 2 points 
            }
        } // LCS()


        /// <summary>Scan the tables of which lines are inserted and deleted,
        /// producing an edit script in forward order.  
        /// </summary>
        /// dynamic array
        private static Item[] CreateDiffs(DiffData DataA, DiffData DataB)
        {
            var a = new List<Item>();
            Item aItem;
            Item[] result;

            int StartA, StartB;
            int LineA, LineB;

            LineA = 0;
            LineB = 0;
            while (LineA < DataA.Length || LineB < DataB.Length)
            {
                if ((LineA < DataA.Length) && (!DataA.modified[LineA])
                  && (LineB < DataB.Length) && (!DataB.modified[LineB]))
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

                    while (LineA < DataA.Length && (LineB >= DataB.Length || DataA.modified[LineA]))
                        // while (LineA < DataA.Length && DataA.modified[LineA])
                        LineA++;

                    while (LineB < DataB.Length && (LineA >= DataA.Length || DataB.modified[LineB]))
                        // while (LineB < DataB.Length && DataB.modified[LineB])
                        LineB++;

                    if ((StartA < LineA) || (StartB < LineB))
                    {
                        // store a new difference-item
                        aItem = new Item();
                        aItem.StartA = StartA;
                        aItem.StartB = StartB;
                        aItem.deletedA = LineA - StartA;
                        aItem.insertedB = LineB - StartB;
                        a.Add(aItem);
                    } // if
                } // if
            } // while

            return a.ToArray();
        }

        /// <summary>Data on one input file being compared.  
        /// </summary>
        internal class DiffData
        {

            /// <summary>Number of elements (lines).</summary>
            internal int Length;

            /// <summary>Buffer of numbers that will be compared.</summary>
            internal int[] data;

            /// <summary>
            /// Array of booleans that flag for modified data.
            /// This is the result of the diff.
            /// This means deletedA in the first Data or inserted in the second Data.
            /// </summary>
            internal bool[] modified;

            /// <summary>
            /// Initialize the Diff-Data buffer.
            /// </summary>
            /// <param name="data">reference to the buffer</param>
            internal DiffData(int[] initData)
            {
                data = initData;
                Length = initData.Length;
                modified = new bool[Length + 2];
            } // DiffData

        } // class DiffData

        /// <summary>details of one difference.</summary>
        public struct Item
        {
            /// <summary>Start Line number in Data A.</summary>
            public int StartA;
            /// <summary>Start Line number in Data B.</summary>
            public int StartB;

            /// <summary>Number of changes in Data A.</summary>
            public int deletedA;
            /// <summary>Number of changes in Data B.</summary>
            public int insertedB;
        } // Item
    }
}
