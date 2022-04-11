using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmpScm.Git.Implementation
{
    internal static class QueryExtensions
    {
        public static IEnumerable<TResult> AsNonAsyncEnumerable<TResult>(this IAsyncEnumerable<TResult> source)
        {
            var e = source.GetAsyncEnumerator();
            try
            {
                while (true)
                {
                    var r = e.MoveNextAsync().AsTask(); // Store as object instead of struct, as we are yield'ing.

                    if (!r.GetAwaiter().GetResult())
                        break;

                    yield return e.Current;
                }
            }
            finally
            {
                if (e is IDisposable d)
                    d.Dispose();
                else
                {
                    var r2 = e.DisposeAsync().AsTask(); // Store as object instead of struct, as we are yield'ing.

                    if (!r2.IsCompleted)
                        r2.Wait();
                }
            }
        }

        public static IEnumerable<TResult> AsNonAsyncEnumerable<TResult>(this IAsyncEnumerator<TResult> source)
        {
            var e = source;
            bool next;
            do
            {
                var r = e.MoveNextAsync().AsTask(); // Store as object instead of struct, as we are yield'ing.
                next = r.Result;

                if (next)
                    yield return e.Current;
            }
            while (next);
        }

#if NETFRAMEWORK || !NET5_0_OR_GREATER
        /// <summary>
        /// Waits asynchronously for the process to exit.
        /// </summary>
        /// <param name="process">The process to wait for cancellation.</param>
        /// <param name="cancellationToken">A cancellation token. If invoked, the task will return 
        /// immediately as canceled.</param>
        /// <returns>A Task representing waiting for the process to end.</returns>
        public static async Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<object?>();
            process.Exited += (sender, args) => tcs.TrySetResult(null);
            process.EnableRaisingEvents = true;
            if (cancellationToken != default(CancellationToken))
                cancellationToken.Register(() => tcs.SetCanceled());

            if (!process.HasExited)
                await tcs.Task.ConfigureAwait(false);
        }
#endif

#if NETFRAMEWORK
        public static bool TryDequeue<TResult>(this Queue<TResult> queue, out TResult value)
        {
            if (queue?.Count > 0)
            {
                value = queue.Dequeue();
                return true;
            }
            else
            {
#pragma warning disable CS8601 // Possible null reference assignment.
                value = default;
#pragma warning restore CS8601 // Possible null reference assignment.
                return false;
            }
        }
#endif
    }
}
