using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmpScm.Buckets.Interfaces;
using AmpScm.Buckets.Specialized;

namespace AmpScm.Buckets
{
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
    partial class BucketExtensions
    {
#if NETFRAMEWORK && !NET48_OR_GREATER
        internal static Task<int> ReceiveAsync(this Socket socket, Memory<byte> buffer, SocketFlags socketFlags)
        {
            if (socket is null)
                throw new ArgumentNullException(nameof(socket));

            return Task<int>.Factory.FromAsync(
                (AsyncCallback cb, object? state) =>
                {
                    var (arr, offset) = BucketBytes.ExpandToArray(buffer);

                    return socket.BeginReceive(arr!, offset, buffer.Length, socketFlags, cb, state);
                },
                socket.EndReceive, null);
        }


        internal static Task<int> SendAsync(this Socket socket, ReadOnlyMemory<byte> buffer, SocketFlags socketFlags)
        {
            if (socket is null)
                throw new ArgumentNullException(nameof(socket));

            return Task<int>.Factory.FromAsync(
                (AsyncCallback cb, object? state) =>
                {
                    var (arr, offset) = BucketBytes.ExpandToArray(buffer);

                    return socket.BeginSend(arr!, offset, buffer.Length, socketFlags, cb, state);
                },
                socket.EndSend, null);
        }

        internal static Task ConnectAsync(this Socket socket, string host, int port)
        {
            if (socket is null)
                throw new ArgumentNullException(nameof(socket));

            return Task.Factory.FromAsync(
                (AsyncCallback cb, object? state) => socket.BeginConnect(host, port, cb, state),
                socket.EndConnect, null);
        }
#endif

        public static ValueTask WriteAsync(this Stream stream, BucketBytes bucketBytes, CancellationToken cancellationToken = default)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

#if !NETFRAMEWORK
            return stream.WriteAsync(bucketBytes.Memory, cancellationToken);
#else
            var (q, r) = bucketBytes;

            if (q is not null)
                return new ValueTask(stream.WriteAsync(q, r, bucketBytes.Length, cancellationToken));
            else
            {
                q = bucketBytes.ToArray();
                return new ValueTask(stream.WriteAsync(q, 0, bucketBytes.Length, cancellationToken));
            }
#endif
        }

        /// <summary>
        /// Writes the bucket to <paramref name="stream"/> and then closes <paramref name="bucket"/>
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="bucket"></param>
        /// <param name="cancellationToken">Token for <see cref="Stream.WriteAsync(byte[], int, int, CancellationToken)"/></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static async ValueTask WriteToAsync(this Bucket bucket, Stream stream, CancellationToken cancellationToken = default)
        {
            if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));
            else if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            using (bucket)
            {
#if NET6_0_OR_GREATER
                if (stream is FileStream fs && fs.CanSeek && bucket is IBucketReadBuffers rb)
                {
                    var handle = fs.SafeFileHandle;
                    long pos = fs.Position;
                    try
                    {
                        while (true)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var (buffers, done) = await bucket.ReadBuffersAsync().ConfigureAwait(false);

                            if (buffers.Length > 0)
                            {
                                var len = buffers.Sum(x => (long)x.Length);

                                await RandomAccess.WriteAsync(handle, buffers, pos, cancellationToken).ConfigureAwait(false);
                                pos += len;
                            }

                            if (done)
                                return;
                        }
                    }
                    finally
                    {
                        fs.Position = pos;
                    }
                }
#endif
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var bb = await bucket.ReadAsync().ConfigureAwait(false);

                    if (bb.IsEof)
                        break;

                    await stream.WriteAsync(bb, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

}

