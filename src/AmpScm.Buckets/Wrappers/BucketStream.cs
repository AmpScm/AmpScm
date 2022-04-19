using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;

namespace AmpScm.Buckets.Wrappers
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "<Pending>")]
    internal partial class BucketStream : Stream
    {
        bool _gotLength;
        long _length;

        public BucketStream(Bucket bucket)
        {
            Bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
        }

        public Bucket Bucket { get; }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                    Bucket.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

#if !NETFRAMEWORK
        public override async ValueTask DisposeAsync()
        {
            Bucket.Dispose();
            await base.DisposeAsync().ConfigureAwait(false);
        }
#endif

        public override bool CanRead => true;

        public override bool CanSeek => Bucket.CanReset;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                if (!_gotLength)
                {
                    _gotLength = true;

                    var p = Bucket.Position;

                    if (!p.HasValue)
                        return -1L;

                    var r = Bucket.ReadRemainingBytesAsync().Result; // BAD async

                    if (r.HasValue)
                        _length = r.Value + p.Value;
                }
                return _length;
            }
        }

        public override long Position { get => Bucket.Position ?? 0L; set => Seek(value, SeekOrigin.Begin); }

        public override void Flush()
        {
            //throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count).Result;
        }


        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await DoReadAsync(new Memory<byte>(buffer, offset, count)).ConfigureAwait(false);
        }

        async ValueTask<int> DoReadAsync(Memory<byte> buffer)
        {
            var r = await Bucket.ReadAsync(buffer.Length).ConfigureAwait(false);

            if (r.IsEof)
                return 0;

            r.CopyTo(buffer);
            return r.Length;
        }

#if !NETFRAMEWORK
#pragma warning disable RS0027 // Public API with optional parameter(s) should have the most parameters amongst its public overloads
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
#pragma warning restore RS0027 // Public API with optional parameter(s) should have the most parameters amongst its public overloads
        {
            return DoReadAsync(buffer);
        }
#endif

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            var valuetask = DoReadAsync(new Memory<byte>(buffer, offset, count));

            if (valuetask.IsCompletedSuccessfully)
            {
                var r = new SyncDone { AsyncState = state, Result = valuetask.Result };

                callback?.Invoke(r);
                return r;
            }
            else
            {
                var task = valuetask.AsTask();
                var tcs = new TaskCompletionSource<int>(state);

                task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        tcs.TrySetException(t.Exception!.InnerExceptions);
                    else if (t.IsCanceled)
                        tcs.TrySetCanceled();
                    else
                    {
                        tcs.TrySetResult(t.Result);
                    }

                    callback?.Invoke(tcs.Task);
                }, TaskScheduler.Default);

                return tcs.Task;
            }
        }

        sealed class SyncDone : IAsyncResult
        {
            public object? AsyncState { get; set; }

            public WaitHandle AsyncWaitHandle => null!;

            public bool CompletedSynchronously => true;

            public bool IsCompleted => true;

            public int Result;
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return (asyncResult as SyncDone)?.Result ?? ((Task<int>)asyncResult).Result;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }
    }
}
