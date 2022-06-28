using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
namespace AmpScm
{
    public static class TestExtensions
    {
        public static void ReadFull(this Bucket self, byte[] array)
        {
            int pos = 0;

            while (pos < array.Length)
            {
                var r = self.ReadAsync(array.Length - pos);

                BucketBytes bb;

                if (r.IsCompleted)
                    bb = r.Result;
                else
                    bb = r.AsTask().Result;

                bb.CopyTo(array, pos);
                pos += bb.Length;
                if (bb.IsEof)
                    throw new InvalidOperationException();
            }
        }

        public static string PerTestDirectory(this TestContext tc, string? subPath = null)
        {
            string dir;
            if (!string.IsNullOrEmpty(subPath))
                dir = Path.Combine(tc.FullyQualifiedTestClassName, tc.TestName, subPath);
            else
                dir = Path.Combine(tc.FullyQualifiedTestClassName, tc.TestName);

            if (dir.Length + tc.TestResultsDirectory.Length > 100)
                dir = Path.Combine(tc.TestResultsDirectory, SHA1String(dir).Substring(0, 10));
            else
                dir = Path.Combine(tc.TestResultsDirectory, dir);

            if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
                dir = dir.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            Directory.CreateDirectory(dir);

            return dir;
        }

        private static string SHA1String(string v)
        {
            using var sha1 = SHA1.Create();
            return FormatHash(sha1.ComputeHash(Encoding.UTF8.GetBytes(v)));
        }

        public static string FormatHash(byte[] hashResult)
        {
            var sb = new StringBuilder();
            foreach (var b in hashResult)
                sb.Append(b.ToString("X2"));

            return sb.ToString();
        }

        public static async ValueTask<byte[]> ReadToEnd(this Bucket self)
        {
            ByteCollector bc = new ByteCollector();

            BucketBytes bb;
            while (!(bb = await self.ReadAsync()).IsEof)
            {
                bc.Append(bb);
            }

            return bc.ToArray();
        }

        public static async ValueTask BucketsEqual(this Assert self, Bucket left, Bucket right)
        {
            Assert.IsTrue(await left.NoDispose().HasSameContentsAsync(right.NoDispose()));
        }

        public static void WriteLine(this TestContext TestContext)
        {
            TestContext.WriteLine("");
        }

        public static void WriteLine(this TestContext TestContext, object value)
        {
            TestContext.WriteLine(value?.ToString() ?? "");
        }


        public static byte[] ReverseInPlaceIfLittleEndian(this byte[] array)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(array);

            return array;
        }

        public static byte[] GetBytesReversedIfLittleEndian(this byte[] array, int index, int length)
        {
            var bytes = new byte[length];
            Array.Copy(array, index, bytes, 0, length);

            return bytes.ReverseInPlaceIfLittleEndian();
        }

    }
}
