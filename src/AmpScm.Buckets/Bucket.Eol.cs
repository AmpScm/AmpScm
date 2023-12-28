using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        Zero = 0x01,
        LF = 0x02,
        CR = 0x04,
        CRLF = 0x08,

        AnyEol = LF | CR | CRLF,

        EolMask = Zero | AnyEol,
        CRSplit = 0x100000
    }

    public sealed class BucketEolState
    {
        internal byte? _kept;

        public bool IsEmpty => !_kept.HasValue;
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly record struct BucketLine(BucketBytes Bytes, BucketEol Eol);

    public partial class Bucket
    {
#if !NETFRAMEWORK
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public virtual async ValueTask<BucketLine> ReadUntilEolAsync(BucketEol acceptableEols, int requested = MaxRead)
        {
            if (requested <= 0)
                throw new ArgumentOutOfRangeException(nameof(requested), requested, message: null);
            if ((acceptableEols & ~BucketEol.EolMask) != 0)
                throw new ArgumentOutOfRangeException(nameof(acceptableEols), acceptableEols, message: null);

            using var pd = await this.PollReadAsync(1).ConfigureAwait(false);

            if (pd.IsEof)
                return new(BucketBytes.Eof, BucketEol.None);

            var (rq, singleCrRequested) = CalculateEolReadLength(acceptableEols, requested, pd.Data.Span);

            var read = await pd.ReadAsync(rq).ConfigureAwait(false);
            var found = GetEolResult(acceptableEols, rq, singleCrRequested, read.Span);

            return new(read, found);
        }

        static readonly ReadOnlyMemory<byte>[] Eols =
{
            Array.Empty<byte>(),
            new byte[] { 0 },
            new byte[] { (byte)'\n'},
            new byte[] { 0, (byte)'\n'},
            new byte[] { (byte)'\r'},
            new byte[] { 0, (byte)'\r' },
            new byte[] { (byte)'\n', (byte)'\r'},
            new byte[] { 0, (byte)'\n', (byte)'\r'},
        };


#if !NETFRAMEWORK
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
#endif
        private static (int Requested, bool SingleCrRequested) CalculateEolReadLength(BucketEol acceptableEols, int requested, ReadOnlySpan<byte> buffer)
        {
            int wantEols = (int)(acceptableEols & (BucketEol.LF | BucketEol.CR | BucketEol.Zero)) | ((int)(acceptableEols & BucketEol.CRLF) >> 1);

            // IndexofAny implements optimizations for 2 and 3 needles, but not for just 1.
            var any = Eols[wantEols].Span;
            int nl = any.Length != 1 ? buffer.IndexOfAny(Eols[wantEols].Span) : buffer.IndexOf(any[0]);

            if (nl < 0)
                return (Math.Min(buffer.Length + 1, requested), false);
            else if ((acceptableEols & ~(BucketEol.LF | BucketEol.CR | BucketEol.Zero)) == 0 || buffer[nl] != '\r')
                return (Math.Min(1 + nl, requested), false);

            return CalculateEolReadLengthForCR(acceptableEols, requested, buffer, nl);
        }

        private static (int Requested, bool SingleCrRequested) CalculateEolReadLengthForCR(BucketEol acceptableEols, int requested, ReadOnlySpan<byte> buffer, int cr)
        {
            if (buffer.Length > cr + 1 && buffer[cr + 1] == '\n')
            {
                return (Math.Min(cr + 2, requested), false); // CR+LF
            }

            int wantEols = (int)(acceptableEols & (BucketEol.LF | BucketEol.Zero));
            int lf = wantEols > 0 ? buffer.Slice(cr + 1).IndexOfAny(Eols[wantEols].Span) : -1;

            if (lf > 0)
                lf += cr + 1;

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

            if (lf > 0 && (lf < cr || cr < 0))
                return (Math.Min(lf + 1, requested), false);
            else if (cr < 0)
                return (Math.Min(buffer.Length + 1, requested), false);

            bool singleCrRequested = false;

            int rq;

            if (cr + 1 < buffer.Length)
            {
                if (buffer[cr + 1] == '\n')
                    rq = cr + 2; // cr+lf
                else
                {
                    rq = cr + 1; // cr without lf
                    singleCrRequested = (acceptableEols & BucketEol.CR) != 0;
                }
            }
            else if (acceptableEols == BucketEol.CRLF)
                rq = buffer.Length + 2; // Need at least two more chars to have a "\r\n"
            else
                rq = buffer.Length + 1; // Need at least one additional character to find some EOL

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
