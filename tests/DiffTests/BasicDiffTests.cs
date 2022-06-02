using AmpScm.Diff;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Text;

namespace DiffTests
{
    [TestClass]
    public class BasicDiffTests
    {
        public TestContext TestContext { get; set; } = default!;

        [TestMethod]
        public void DiffSame()
        {
            var diff = Differences.Calculate(new StringTokenizer(), new[] { "A", "B", "C" }, new[] { "A", "B", "C" });
            Assert.IsNotNull(diff);
            Assert.IsFalse(diff.HasConflicts);
            Assert.IsFalse(diff.HasChanges);
            Assert.AreEqual(1.0, diff.Similarity);
            Assert.AreEqual(3, diff.Sum(x => x.Original.Length));
            Assert.AreEqual(3, diff.Sum(x => x.Modified.Length));

            diff = Differences.Calculate(new StringTokenizer(), Array.Empty<string>(), Enumerable.Empty<string>());
            Assert.IsNotNull(diff);
            Assert.IsFalse(diff.HasConflicts);
            Assert.IsFalse(diff.HasChanges);
            Assert.IsFalse(diff.Any());

            diff = Differences.Calculate(new StringTokenizer(), new[] { "" }, new[] { "" });
            Assert.IsNotNull(diff);
            Assert.IsFalse(diff.HasConflicts);
            Assert.IsFalse(diff.HasChanges);
            Assert.AreEqual(1, diff.Sum(x => x.Original.Length));
            Assert.AreEqual(1, diff.Sum(x => x.Modified.Length));
        }

        [TestMethod]
        public void DiffSame3()
        {
            var diff = Differences.Calculate(new StringTokenizer(), new[] { "A", "B", "C" }, new[] { "A", "B", "C" }, new[] {"A", "B", "C"});
            Assert.IsNotNull(diff);
            Assert.IsFalse(diff.HasConflicts);
            Assert.IsFalse(diff.HasChanges);
            Assert.AreEqual(1.0, diff.Similarity);
            Assert.AreEqual(3, diff.Sum(x => x.Original.Length));
            Assert.AreEqual(3, diff.Sum(x => x.Modified.Length));
            Assert.AreEqual(3, diff.Sum(x => x.Latest!.Value.Length));

            diff = Differences.Calculate(new StringTokenizer(), Array.Empty<string>(), Enumerable.Empty<string>(), Array.Empty<string>());
            Assert.IsNotNull(diff);
            Assert.IsFalse(diff.HasConflicts);
            Assert.IsFalse(diff.HasChanges);
            Assert.IsFalse(diff.Any());

            diff = Differences.Calculate(new StringTokenizer(), new[] { "" }, new[] { "" }, new[] {""});
            Assert.IsNotNull(diff);
            Assert.IsFalse(diff.HasConflicts);
            Assert.IsFalse(diff.HasChanges);
            Assert.AreEqual(1, diff.Sum(x => x.Original.Length));
            Assert.AreEqual(1, diff.Sum(x => x.Modified.Length));
            Assert.AreEqual(1, diff.Sum(x => x.Latest!.Value.Length));
        }

        [TestMethod]
        public void DiffDifferent()
        {
            var t = new StringTokenizer();
            var diff = Differences.Calculate(t, new[] { "A", "B", "C" }, new[] { "A", "B", "B'", "C" });
            Assert.IsNotNull(diff);
            Assert.IsFalse(diff.HasConflicts);
            Assert.IsTrue(diff.HasChanges);
            Assert.AreEqual(3, diff.Sum(x => x.Original.Length));
            Assert.AreEqual(4, diff.Sum(x => x.Modified.Length));
            Assert.IsTrue(diff.Similarity >= 0.75, $"Similarity > 75% ({diff.Similarity})");

            diff = Differences.Calculate(t, new string[] { }, new[] { "A" });
            Assert.IsNotNull(diff);
            Assert.IsFalse(diff.HasConflicts);
            Assert.IsTrue(diff.HasChanges);
            Assert.AreEqual(0, diff.Sum(x => x.Original.Length));
            Assert.AreEqual(1, diff.Sum(x => x.Modified.Length));
        }

        [TestMethod]
        public void DiffDifferent3()
        {
            var t = new StringTokenizer();
            var diff = Differences.Calculate(t, new[] { "A", "B", "C" }, new[] { "A", "B", "B'", "C" }, new[] {"A", "B", "D", "C"});
            Assert.IsNotNull(diff);
            Assert.IsFalse(diff.HasConflicts);
            Assert.IsTrue(diff.HasChanges);
            Assert.AreEqual(3, diff.Sum(x => x.Original.Length));
            Assert.AreEqual(4, diff.Sum(x => x.Modified.Length));
            Assert.AreEqual(4, diff.Sum(x => x.Latest!.Value.Length));
            Assert.IsTrue(diff.Similarity >= 0.75, $"Similarity > 75% ({diff.Similarity})");
        }

        [TestMethod]
        public void RunMatherTel()
        {
            string a, b;


            TestContext.WriteLine("Diff Self Test...");

            // test all changes
            a = "a,b,c,d,e,f,g,h,i,j,k,l".Replace(',', '\n');
            b = "0,1,2,3,4,5,6,7,8,9".Replace(',', '\n');
            Assert.AreEqual("12.10.0.0*", TestHelper(DiffText(a, b)),
              "all-changes test failed.");
            TestContext.WriteLine("all-changes test passed.");
            // test all same
            a = "a,b,c,d,e,f,g,h,i,j,k,l".Replace(',', '\n');
            b = a;
            Assert.AreEqual("", TestHelper(DiffText(a, b)),
              "all-same test failed.");
            TestContext.WriteLine("all-same test passed.");

            // test snake
            a = "a,b,c,d,e,f".Replace(',', '\n');
            b = "b,c,d,e,f,x".Replace(',', '\n');
            Assert.AreEqual("1.0.0.0*0.1.6.5*", TestHelper(DiffText(a, b)),
              "snake test failed.");
            TestContext.WriteLine("snake test passed.");

            // 2002.09.20 - repro
            a = "c1,a,c2,b,c,d,e,g,h,i,j,c3,k,l".Replace(',', '\n');
            b = "C1,a,C2,b,c,d,e,I1,e,g,h,i,j,C3,k,I2,l".Replace(',', '\n');
            Assert.AreEqual("1.1.0.0*1.1.2.2*0.2.7.7*1.1.11.13*0.1.13.15*", TestHelper(DiffText(a, b)),
              "repro20020920 test failed.");
            TestContext.WriteLine("repro20020920 test passed.");

            // 2003.02.07 - repro
            a = "F".Replace(',', '\n');
            b = "0,F,1,2,3,4,5,6,7".Replace(',', '\n');
            Assert.AreEqual("0.1.0.0*0.7.1.2*", TestHelper(DiffText(a, b)),
              "repro20030207 test failed.");
            TestContext.WriteLine("repro20030207 test passed.");

            // Muegel - repro
            a = "HELLO\nWORLD";
            b = "\n\nhello\n\n\n\nworld\n";
            Assert.AreEqual("2.8.0.0*", TestHelper(DiffText(a, b)),
              "repro20030409 test failed.");
            TestContext.WriteLine("repro20030409 test passed.");

            // test some differences
            a = "a,b,-,c,d,e,f,f".Replace(',', '\n');
            b = "a,b,x,c,e,f".Replace(',', '\n');
            Assert.AreEqual("1.1.2.2*1.0.4.4*1.0.7.6*", TestHelper(DiffText(a, b)),
              "some-changes test failed.");
            TestContext.WriteLine("some-changes test passed.");

            // test one change within long chain of repeats
            a = "a,a,a,a,a,a,a,a,a,a".Replace(',', '\n');
            b = "a,a,a,a,-,a,a,a,a,a".Replace(',', '\n');
            Assert.AreEqual("0.1.4.4*1.0.9.10*", TestHelper(DiffText(a, b)),
              "long chain of repeats test failed.");

            TestContext.WriteLine("End.");
            //TestContext.Flush();
        }

        private DiffRange[] DiffText(string a, string b)
        {
            var d = Differences.Calculate(StringComparer.OrdinalIgnoreCase, a.Split('\n'), b.Split('\n'));

            return d.ToArray();
        }

        public static string TestHelper(DiffRange[] f)
        {
            StringBuilder ret = new StringBuilder();
            foreach(var r in f)
            {
                if (r.Type == HunkType.Same)
                    continue;

                ret.Append(r.Original.Length.ToString() + "." + r.Modified.Length.ToString() + "." + r.Original.Start.ToString() + "." + r.Modified.Start.ToString() + "*");
            }
            // Debug.Write(5, "TestHelper", ret.ToString());
            return (ret.ToString());
        }
    }
}
