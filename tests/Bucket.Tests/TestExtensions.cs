using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
namespace System
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
                    bb = r.GetAwaiter().GetResult();

                bb.CopyTo(new Memory<byte>(array, pos, bb.Length));
                pos += bb.Length;
                if (bb.IsEof)
                    throw new InvalidOperationException();
            }
        }

        public static string PerTestDirectory(this TestContext tc, string subPath = null)
        {
            string dir;
            if (!string.IsNullOrEmpty(subPath))
                dir = Path.Combine(tc.TestResultsDirectory, tc.FullyQualifiedTestClassName, tc.TestName, subPath);
            else
                dir = Path.Combine(tc.TestResultsDirectory, tc.FullyQualifiedTestClassName, tc.TestName);

            if (dir.Length > 100)
                dir = Path.Combine(tc.TestResultsDirectory, SHA1String(dir).Substring(0, 8));

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
            List<byte> bytes = new List<byte>();
            BucketBytes bb;
            while (!(bb = await self.ReadAsync()).IsEof)
            {
                bytes.AddRange(bb.ToArray());
            }

            return bytes.ToArray();
        }

        public static async ValueTask BucketsEqual(this Assert self, Bucket left, Bucket right)
        {
            long p = 0;

            while (true)
            {
                var l1 = await left.ReadAsync(1);
                var r1 = await right.ReadAsync(1);

                if (l1.IsEof || r1.IsEof)
                {
                    if (l1.IsEof && r1.IsEof)
                        return;

                    Assert.AreEqual(l1.IsEof, r1.IsEof, $"Expected both EOF at same time, pos={p}");
                }

                Assert.AreEqual(1, l1.Length, "Expected 1 byte left");
                Assert.AreEqual(1, r1.Length, "Expected 1 byte right");

                Assert.AreEqual(l1[0], r1[0], $"Expected bytes equal at position {p}");
                p++;

                //TestContext.Write($"{l1[0]:x2} ");
                //
                //if (p % 16 == 0)
                //    TestContext.WriteLine();
            }
        }

        public static void WriteLine(this TestContext TestContext)
        {
            TestContext.WriteLine("");
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
