﻿using System;
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

        public static async ValueTask WriteAsync(this Stream stream, Bucket bucket, CancellationToken cancellationToken = default)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));
            else if (bucket is null)
                throw new ArgumentNullException(nameof(bucket));

            using (bucket)
                while (true)
                {
                    var bb = await bucket.ReadAsync().ConfigureAwait(false);

                    if (bb.IsEof)
                        break;

                    await stream.WriteAsync(bb, cancellationToken).ConfigureAwait(false);
                }
        }
    }

}

