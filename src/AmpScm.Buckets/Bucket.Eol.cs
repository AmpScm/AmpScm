using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets
{
    [Flags]
#pragma warning disable CA2217 // Do not mark enums with FlagsAttribute
    public enum BucketEol
#pragma warning restore CA2217 // Do not mark enums with FlagsAttribute
    {
        None = 0x00,
        LF = 0x01,
        CR = 0x02,
        CRLF = 0x04,
        Zero = 0x08,


        AnyEol = LF | CR | CRLF,

        EolMask = 0xFF,
        CRSplit = 0x100000
    }

    public sealed class BucketEolState
    {
        internal byte? _kept;

        public bool IsEmpty => !_kept.HasValue;
    }

    public partial class Bucket
    {
#if !NETFRAMEWORK
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public virtual async ValueTask<(BucketBytes, BucketEol)> ReadUntilEolAsync(BucketEol acceptableEols, int requested = MaxRead)
        {
            if ((acceptableEols & ~BucketEol.EolMask) != 0)
                throw new ArgumentOutOfRangeException(nameof(acceptableEols));

            using var pd = await this.PollReadAsync(1).ConfigureAwait(false);

            if (pd.IsEof)
                return (BucketBytes.Eof, BucketEol.None);

            var (rq, singleCrRequested) = CalculateEolReadLength(acceptableEols, requested, pd.Data.Span);

            var read = await pd.ReadAsync(rq).ConfigureAwait(false);
            var found = GetEolResult(acceptableEols, rq, singleCrRequested, read.Span);

            return (read, found);
        }

#if !NETFRAMEWORK
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
#endif
        private static (int Requested, bool SingleCrRequested) CalculateEolReadLength(BucketEol acceptableEols, int requested, ReadOnlySpan<byte> buffer)
        {
            if ((acceptableEols & ~(BucketEol.LF | BucketEol.CR | BucketEol.Zero)) == 0)
            {
                int n;
                switch (acceptableEols)
                {
                    case BucketEol.LF:
                        n = buffer.IndexOf((byte)'\n');
                        return (1 + (n >= 0 ? n : buffer.Length), false);
                    case BucketEol.Zero:
                        n = buffer.IndexOf((byte)'\0');
                        return (1 + (n >= 0 ? n : buffer.Length), false);
                    case BucketEol.CR:
                        n = buffer.IndexOf((byte)'\r');
                        return (1 + (n >= 0 ? n : buffer.Length), false);
                }
            }
            int cr = (0 != (acceptableEols & (BucketEol.CR | BucketEol.CRLF))) ? buffer.IndexOf((byte)'\r') : -1;
            int lf = (0 != (acceptableEols & BucketEol.LF)) ? buffer.IndexOf((byte)'\n') : -1;
            int zr = (0 != (acceptableEols & BucketEol.Zero)) ? buffer.IndexOf((byte)'\0') : -1;

            // Fold zero in lf
            lf = (lf >= 0 && zr >= 0) ? Math.Min(lf, zr) : (lf >= 0 ? lf : zr);

            if (cr >= 0 && (acceptableEols & (BucketEol.CR | BucketEol.CRLF)) == BucketEol.CRLF)
            {
                // If we have a cr but not a cr+lf, we want to check the next cr (if any)
                while (cr >= 0 && (lf < 0 || cr < lf) && (cr + 1 < buffer.Length) && buffer[cr + 1] != '\n')
                {
                    var s = buffer.Slice(cr + 1);

                    int n = s.IndexOf((byte)'\r');

                    if (n >= 0)
                        cr += 1 + n;
                    else
                        cr = -1;
                }
            }

            // fold lf (and zero) in cr
            cr = (cr >= 0 && lf >= 0) ? Math.Min(cr, lf) : (cr >= 0 ? cr : lf);

            int linelen = cr;
            bool singleCrRequested = false;

            int rq;

            if (cr >= 0
                && buffer[cr] == '\r'
                && (acceptableEols & BucketEol.CRLF) != 0
                && linelen + 1 < buffer.Length)
            {
                if (buffer[cr + 1] == '\n')
                {
                    rq = linelen + 2; // cr+lf
                }
                else if ((acceptableEols & BucketEol.CRLF) != 0)
                {
                    rq = linelen + 1; // cr without lf
                    singleCrRequested = true;
                }
                else
                {
                    // easy out. Just include the single character after the cr
                    rq = linelen + 2; // cr+lf
                }
            }
            else if (cr >= 0)
            {
                rq = linelen + 1;
            }
            else if (acceptableEols == BucketEol.CRLF)
                rq = buffer.Length + 2; // No newline in rq_len, and we need 2 chars for eol
            else
                rq = buffer.Length + 1; // No newline in rq_len, and we need 1 char for eol

            return (Math.Min(rq, requested), singleCrRequested);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BucketEol GetEolResult(BucketEol acceptableEols, int requested, bool singleCrRequested, ReadOnlySpan<byte> read)
        {
            BucketEol found;

            if (read.IsEmpty /* || read.IsEof */)
                return BucketEol.None;

            byte last = read[read.Length - 1];
            if (last == '\n' && 0 != (acceptableEols & BucketEol.CRLF) && read.Length >= 2 && read[read.Length - 2] == '\r')
            {
                found = BucketEol.CRLF;
            }
            else if (last == '\n' && 0 != (acceptableEols & BucketEol.LF))
            {
                found = BucketEol.LF;
            }
            else if (last == '\r' && BucketEol.CR == (acceptableEols & (BucketEol.CR | BucketEol.CRLF)))
            {
                found = BucketEol.CR;
            }
            else if (last == '\r' && 0 != (acceptableEols & BucketEol.CRLF))
            {
                if (singleCrRequested && requested == read.Length)
                    found = BucketEol.CR;
                else
                    found = BucketEol.CRSplit;
            }
            else if (last == '\0' && 0 != (acceptableEols & BucketEol.Zero))
            {
                found = BucketEol.Zero;
            }
            else
            {
                found = BucketEol.None;
            }

            return found;
        }

        public static BucketFactory Create => BucketFactory.Instance;
    }
}
