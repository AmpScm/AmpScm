using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Wrappers
{
    internal partial class BucketStream
    {
        public sealed class WithWriter : BucketStream
        {
            private IBucketWriter InnerWriter { get; }
            public WithWriter(Bucket bucket, IBucketWriter writer)
                : base(bucket)
            {
                InnerWriter = writer ?? throw new ArgumentNullException(nameof(writer));
            }

            private void DoWriteBucket(Bucket bucket)
            {
                InnerWriter.Write(bucket);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                Write(new ReadOnlySpan<byte>(buffer, offset, count));
            }

#if !NETFRAMEWORK
            public override void Write(ReadOnlySpan<byte> buffer)
#else
            internal void Write(ReadOnlySpan<byte> buffer)
#endif
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                DoWriteBucket(buffer.ToArray().AsBucket());
#pragma warning restore CA2000 // Dispose objects before losing scope
            }


            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Write(new ReadOnlySpan<byte>(buffer, offset, count));

                return Task.CompletedTask;
            }

#if !NETFRAMEWORK
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
#else
            internal ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
#endif

            {
                Write(buffer.Span);

                return default;
            }

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            {
                Write(new ReadOnlySpan<byte>(buffer, offset, count));

                var done = new SyncDone { AsyncState = state };

                callback?.Invoke(done);

                return done;
            }

            public override void EndWrite(IAsyncResult asyncResult)
            {
            }

            public override void Flush()
            {
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public override bool CanWrite => true;
        }
    }
}
