using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Specialized
{
    internal sealed class TraceBucket : Bucket, IBucketPoll, IBucketNoDispose
    {
        private static int _idNext;

        private Bucket Source { get; }
        private int Id { get; }

        private int _nDispose;
        private string? _indent;
        private readonly string _name;

        public TraceBucket(Bucket source, string? name=null)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Id = Interlocked.Increment(ref _idNext);

            _name = name ?? Source.Name; ;
            Trace.WriteLine($"{Ident}0x{Id:x2} tracing read from");
            
        }

        public override string Name => "Trace>" + Source.Name;

        private string Ident => _indent ??= $"{new string(' ', Source.Name.Count(x => x == '>'))}{_name}/0x{Id:x3}:";

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (_nDispose-- == 0)
{
                    Trace.WriteLine($"{Ident} disposing");
                    Source.Dispose();
                }
                else
                    Trace.WriteLine($"{Ident} ignoring dispose {Source.Name}");
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        Bucket IBucketNoDispose.NoDispose()
        {
            _nDispose++;
            return this;
        }

        bool IBucketNoDispose.HasMultipleDisposers()
        {
            return _nDispose > 1;
        }

        public override BucketBytes Peek()
        {
            var bb = Source.Peek();

            Trace.WriteLine($"{Ident} peeking {bb.Length} bytes{(bb.IsEof ? ", eof=True" : "")} {Sum(bb)}");
            return bb;
        }

        public override async ValueTask<BucketBytes> ReadAsync(int requested = MaxRead)
        {
            var bb = await Source.ReadAsync(requested).ConfigureAwait(false);

            Trace.WriteLine($"{Ident} reading {bb.Length}/{requested} bytes{(bb.IsEof ? ", eof=True" : "")} {Sum(bb)}");
            return bb;
        }

        public override async ValueTask<long?> ReadRemainingBytesAsync()
        {
            long? l = await Source.ReadRemainingBytesAsync().ConfigureAwait(false);

            Trace.WriteLine($"{Ident} reading {l ?? -1L} bytes remaining");

            return l;
        }

        async ValueTask<BucketBytes> IBucketPoll.PollAsync(int minRequested)
        {
            var bb = await Source.PollAsync(minRequested).ConfigureAwait(false);

            Trace.WriteLine($"{Ident} polling {bb.Length}/{minRequested} bytes{(bb.IsEof ? ", eof=True" : "")} {Sum(bb)}");
            return bb;
        }

        private static string Sum(BucketBytes bb)
        {
            if (bb.IsEmpty)
                return bb.IsEof ? "<EOF>" : "<Empty>";

            StringBuilder sb = new StringBuilder();


            sb.Append('"');

            if (bb.Length < 80)
                DumpBB(sb, bb);
            else
            {
                DumpBB(sb, bb.Slice(0, 20));
                sb.Append("..<snip>..");
                DumpBB(sb, bb.Slice(bb.Length - 20));
            }

            sb.Append('"');

            return sb.ToString();
        }

        private static void DumpBB(StringBuilder sb, BucketBytes bb)
        {
            for (int i = 0; i < bb.Length; i++)
            {
                byte b = bb[i];

                if (b < 0x80 && !char.IsControl((char)b) && !char.IsWhiteSpace((char)b))
                    sb.Append((char)b);
                else if (b == '\n')
                    sb.Append("\\n");
                else if (b == '\r')
                    sb.Append("\\r");
                else if (char.IsWhiteSpace((char)b))
                    sb.Append(' ');
                else
                    sb.Append('.');
            }
        }
    }
}
