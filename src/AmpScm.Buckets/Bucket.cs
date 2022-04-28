﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;

[assembly: CLSCompliant(true)]

namespace AmpScm.Buckets
{
    /// <summary>
    /// Generic bucket. A bucket is a stream of data that can be read without explicit copying. Buckets are typically
    /// stacked on top of each other to do processing.
    /// </summary>
    /// <remarks>The result of most function calls (especially <see cref="ReadAsync"/>, <see cref="Peek"/> and all variants)
    /// and the data /// references by the return values of these functions is typically valid until the next read operation.
    ///
    /// <para>While .Net guaranteers that there is still data referenced, the data might have been replaced by the next
    /// call. So in general, once you hand over a bucket to some other user you shouldn't access the bucket until
    /// the other user is done. Most users will call <see cref="Dispose()"/> on buckets that are handed over, so
    /// wrapping a bucket using <see cref="BucketExtensions.NoClose"/> might be useful.</para>
    /// </remarks>
    [DebuggerDisplay("{Name}: Position={Position}")]
    public abstract partial class Bucket : IDisposable
    {
        /// <summary>
        /// Static bucket that doesn't contain any data
        /// </summary>
        public static readonly Bucket Empty = new EmptyBucket();


        protected internal static readonly ValueTask<BucketBytes> EofTask = new ValueTask<BucketBytes>(BucketBytes.Eof);
        protected internal static readonly ValueTask<BucketBytes> EmptyTask = new ValueTask<BucketBytes>(BucketBytes.Empty);

        /// <summary>
        /// When inherited creates a new bucket
        /// </summary>
        protected Bucket()
        {

        }

        /// <summary>
        /// Gets a name describing the bucket. Typically contains a chain of names useful for debugging purposes
        /// or error reporting
        /// </summary>
        public virtual string Name
        {
            get => BaseName;
        }

        internal string BaseName
        {
            get
            {
                string name = GetType().Name;

                if (name.Length > 6 && name.EndsWith("Bucket", StringComparison.OrdinalIgnoreCase))
                    return name.Substring(0, name.Length - 6);
                else
                    return name;
            }
        }

        /// <summary>
        /// The main read function. Returns anywhere from 1 upto <paramref name="requested"/> bytes of data, or
        /// reports <see cref="BucketBytes.Eof"/>. Calling this function implicitly invalidates results of
        /// previous reads and/or peeks.
        /// </summary>
        /// <param name="requested">The maximum number of bytes acceptable to caller</param>
        /// <returns></returns>
        /// <remarks>This function and any inherited variant MUST NEVER return 0 bytes, and success</remarks>
        public abstract ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue);

        /// <summary>
        /// If implemented by the bucket provides a peek into the next data that can be read from the bucket
        /// with the next <see cref="ReadAsync"/> call. The next call will typically return the same data or
        /// a bit more. Peek is not expected to do any processing or Polling (See:
        /// <see cref="BucketExtensions.PollAsync(Bucket, int)"/>, for explicit peek-like polling)
        /// </summary>
        /// <returns></returns>
        public virtual BucketBytes Peek()
        {
            return BucketBytes.Empty;
        }

        /// <summary>
        /// Tries to read <paramref name="requested"/> bytes from the bucket. Returns the number of bytes
        /// read when reading completed by EOF or completing the total number.
        /// </summary>
        /// <param name="requested"></param>
        /// <returns></returns>
        /// <remarks>The default implementation performs this as calling <see cref="ReadAsync(int)"/> as
        /// many times as necessary. But callers may optimize this</remarks>
        public virtual ValueTask<long> ReadSkipAsync(long requested)
        {
            return SkipByReading(requested);
        }

        /// <summary>Wrapper around <see cref="ReadSkipAsync(long)"/></summary>
        /// <param name="requested"></param>
        /// <returns></returns>
        public async ValueTask<int> ReadSkipAsync(int requested)
        {
            return (int)await ReadSkipAsync((long)requested).ConfigureAwait(false);
        }

        internal async ValueTask<long> SkipByReading(long requested)
        {
            long skipped = 0;
            while (requested > 0)
            {
                var v = await ReadAsync((int)Math.Min(requested, int.MaxValue)).ConfigureAwait(false);
                if (v.Length == 0)
                    break;

                requested -= v.Length;
                skipped += v.Length;
            }
            return skipped;
        }

        /// <summary>
        /// Tries to read the number of bytes remaining from the bucket if the bucket 'somehow' knows
        /// this number. This is 'Read' operation, to allow reading some header data to obtain the
        /// information.
        /// </summary>
        /// <returns>The number of bytes remaining after returning from this call or <c>null</c> if
        /// unavailable/unknown.</returns>
        public virtual ValueTask<long?> ReadRemainingBytesAsync()
        {
            return new ValueTask<long?>((long?)null);
        }

        /// <summary>
        /// If this bucket keeps track of positioning, returns the current position in bytes from
        /// counting from the initial readable byte.
        /// </summary>
        public virtual long? Position => null;

        /// <summary>
        /// If implemented returns a new bucket holding independently the same data as this bucket.
        /// When <paramref name="reset"/> is true, the already read data can be read again from the
        /// resulting bucket.
        /// </summary>
        /// <param name="reset">Start reading at end</param>
        /// <returns>A new bucket</returns>
        /// <exception cref="InvalidOperationException">Reset is not supported on bucket</exception>
        /// <exception cref="NotSupportedException">Duplicating this bucket is not supported</exception>
        /// <remarks>Caller should take care of calling <see cref="Dispose()"/> on the returned bucket</remarks>
        public virtual ValueTask<Bucket> DuplicateAsync(bool reset = false)
        {
            if (reset && !CanReset)
                throw new InvalidOperationException($"Reset not supported on {Name} bucket");

            throw new NotSupportedException($"DuplicateAsync not implemented on {Name} bucket");
        }

        /// <summary>
        /// Gets a boolean indictating whether <see cref="ResetAsync"/> is supported.
        /// </summary>
        public virtual bool CanReset => false;

        /// <summary>
        /// If implemented returns the bucket to its original location so data can be read again
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Reset is not supported. (See <cref name="CanReset" /></exception>
        public virtual ValueTask ResetAsync()
        {
            if (!CanReset)
                throw new InvalidOperationException($"Reset not supported on {Name} bucket");

            return default;
        }

        /// <summary>
        /// Try reading a bucket of data as a whole from the start of the bucket
        /// </summary>
        /// <typeparam name="TBucket"></typeparam>
        /// <returns></returns>
        public virtual TBucket? ReadBucket<TBucket>()
            where TBucket : Bucket
        {
            return default;
        }

        sealed class EmptyBucket : Bucket
        {
            public override ValueTask<BucketBytes> ReadAsync(int requested = int.MaxValue)
            {
                return EofTask;
            }
        }

        /// <summary>
        /// Cleans up resources hold by the bucket, including possibly hold inner buckets
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Cleans up resources hold by the bucket, including possibly hold inner buckets
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// When not overridden returns <see cref="Name"/>
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// If both <paramref name="first"/> and <paramref name="second"/> are not null, return
        /// a bucket holding both. If either is null, return the other.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        /// <remarks>If <paramref name="first"/> or <paramref name="second"/> is an aggregate bucket,
        /// insert the other in the existing aggregate. Otherwise creates a new aggregate bucket</remarks>
#pragma warning disable CA2225 // Operator overloads have named alternates
        public static Bucket operator +(Bucket first, Bucket second)
#pragma warning restore CA2225 // Operator overloads have named alternates
        {
            if (first is null || first is EmptyBucket)
                return second ?? Bucket.Empty;
            else if (second is null || second is EmptyBucket)
                return first;
            else
                return first.Append(second);
        }
    }
}
