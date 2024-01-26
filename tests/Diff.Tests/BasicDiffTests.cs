using AmpScm.Diff;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]


namespace DiffTests;

[TestClass]
public class BasicDiffTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public void DiffSame()
    {
        var diff = DifferenceSet.Calculate(StringComparer.Ordinal, new[] { "A", "B", "C" }, new[] { "A", "B", "C" });
        Assert.IsNotNull(diff);
        Assert.IsFalse(diff.HasConflicts);
        Assert.IsFalse(diff.HasChanges);
        Assert.AreEqual(1.0, diff.Similarity);
        Assert.AreEqual(3, diff.Sum(x => x.Original.Length));
        Assert.AreEqual(3, diff.Sum(x => x.Modified.Length));

        diff = DifferenceSet.Calculate(StringComparer.Ordinal, Array.Empty<string>(), Enumerable.Empty<string>());
        Assert.IsNotNull(diff);
        Assert.IsFalse(diff.HasConflicts);
        Assert.IsFalse(diff.HasChanges);
        Assert.IsFalse(diff.Any());

        diff = DifferenceSet.Calculate(StringComparer.Ordinal, new[] { "" }, new[] { "" });
        Assert.IsNotNull(diff);
        Assert.IsFalse(diff.HasConflicts);
        Assert.IsFalse(diff.HasChanges);
        Assert.AreEqual(1, diff.Sum(x => x.Original.Length));
        Assert.AreEqual(1, diff.Sum(x => x.Modified.Length));
    }

    [TestMethod]
    public void DiffSame3()
    {
        var diff = DifferenceSet.Calculate(StringComparer.Ordinal, new[] { "A", "B", "C" }, new[] { "A", "B", "C" }, new[] { "A", "B", "C" });
        Assert.IsNotNull(diff);
        Assert.IsFalse(diff.HasConflicts);
        Assert.IsFalse(diff.HasChanges);
        Assert.AreEqual(1.0, diff.Similarity);
        Assert.AreEqual(3, diff.Sum(x => x.Original.Length));
        Assert.AreEqual(3, diff.Sum(x => x.Modified.Length));
        Assert.AreEqual(3, diff.Sum(x => x.Latest!.Value.Length));

        diff = DifferenceSet.Calculate(StringComparer.Ordinal, Array.Empty<string>(), Enumerable.Empty<string>(), Array.Empty<string>());
        Assert.IsNotNull(diff);
        Assert.IsFalse(diff.HasConflicts);
        Assert.IsFalse(diff.HasChanges);
        Assert.IsFalse(diff.Any());

        diff = DifferenceSet.Calculate(StringComparer.Ordinal, new[] { "" }, new[] { "" }, new[] { "" });
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
        var diff = DifferenceSet.Calculate(StringComparer.Ordinal, new[] { "A", "B", "C" }, new[] { "A", "B", "B'", "C" });
        Assert.IsNotNull(diff);
        Assert.IsFalse(diff.HasConflicts);
        Assert.IsTrue(diff.HasChanges);
        Assert.AreEqual(3, diff.Sum(x => x.Original.Length));
        Assert.AreEqual(4, diff.Sum(x => x.Modified.Length));
        Assert.IsTrue(diff.Similarity >= 0.75, $"Similarity > 75% ({diff.Similarity})");

        diff = DifferenceSet.Calculate(StringComparer.Ordinal, Array.Empty<string>(), new[] { "A" });
        Assert.IsNotNull(diff);
        Assert.IsFalse(diff.HasConflicts);
        Assert.IsTrue(diff.HasChanges);
        Assert.AreEqual(0, diff.Sum(x => x.Original.Length));
        Assert.AreEqual(1, diff.Sum(x => x.Modified.Length));




        diff = DifferenceSet.Calculate(StringComparer.Ordinal,
            new[] { "A", "B", "C", "D", "E", "F", "G", "H" },
            new[] { "A", "B", "Z", "A", "C", "D", "E", "V", "V", "Q", "F", "G", "H" });
        Assert.IsNotNull(diff);
        Assert.IsFalse(diff.HasConflicts);
        Assert.IsTrue(diff.HasChanges);
        Assert.AreEqual(8, diff.Sum(x => x.Original.Length));
        Assert.AreEqual(13, diff.Sum(x => x.Modified.Length));
        Assert.IsTrue(diff.Similarity >= 0.60, $"Similarity > 60% ({diff.Similarity})");
    }

    [TestMethod]
    public void DiffDifferent3()
    {
        var diff = DifferenceSet.Calculate(StringComparer.Ordinal, new[] { "A", "B", "C" }, new[] { "A", "B", "B'", "C" }, new[] { "A", "B", "D", "C" });
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


        // test all changes
        a = "a,b,c,d,e,f,g,h,i,j,k,l".Replace(',', '\n');
        b = "0,1,2,3,4,5,6,7,8,9".Replace(',', '\n');
        Assert.AreEqual("[0,11]->[0,9]", TestHelper(DiffText(a, b)), "all-changes");

        // test all same
        a = "a,b,c,d,e,f,g,h,i,j,k,l".Replace(',', '\n');
        b = a;
        Assert.AreEqual("", TestHelper(DiffText(a, b)), "all-same");

        // test snake
        a = "a,b,c,d,e,f".Replace(',', '\n');
        b = "b,c,d,e,f,x".Replace(',', '\n');
        Assert.AreEqual("[0]->(0) (6)->[5]", TestHelper(DiffText(a, b)), "snake");

        // 2002.09.20 - repro
        a = "c1,a,c2,b,c,d,e,g,h,i,j,c3,k,l".Replace(',', '\n');
        b = "C1,a,C2,b,c,d,e,I1,e,g,h,i,j,C3,k,I2,l".Replace(',', '\n');
        Assert.AreEqual("[0]->[0] [2]->[2] (7)->[7,8] [11]->[13] (13)->[15]", TestHelper(DiffText(a, b)), "repro20020920");

        // 2003.02.07 - repro
        a = "F".Replace(',', '\n');
        b = "0,F,1,2,3,4,5,6,7".Replace(',', '\n');
        Assert.AreEqual("(0)->[0] (1)->[2,8]", TestHelper(DiffText(a, b)), "repro20030207");
        TestContext.WriteLine("repro20030207 test passed.");

        // Muegel - repro
        a = "HELLO\nWORLD";
        b = "\n\nhello\n\n\n\nworld\n";
        Assert.AreEqual("[0,1]->[0,7]", TestHelper(DiffText(a, b)), "repro20030409");

        // test some differences
        a = "a,b,-,c,d,e,f,f".Replace(',', '\n');
        b = "a,b,x,c,e,f".Replace(',', '\n');
        Assert.AreEqual("[2]->[2] [4]->(4) [7]->(6)", TestHelper(DiffText(a, b)), "some-changes");

        // test one change within long chain of repeats
        a = "a,a,a,a,a,a,a,a,a,a".Replace(',', '\n');
        b = "a,a,a,a,-,a,a,a,a,a".Replace(',', '\n');
        Assert.AreEqual("(4)->[4] [9]->(10)", TestHelper(DiffText(a, b)), "long chain of repeats");
    }

    private DiffChunk[] DiffText(string a, string b)
    {
        var d = DifferenceSet.Calculate(StringComparer.Ordinal, a.Split('\n'), b.Split('\n'));

        return d.ToArray();
    }

    private static Lazy<string?> SvnExe { get; } = new (() =>
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var f = Path.Combine(Path.GetFullPath(dir), "svn");

            if (File.Exists(f))
                return f;
            else if (File.Exists(f += ".exe"))
                return f;
        }

        return null;
    });

    [TestMethod]
    public void DiffFileLikeSubversion()
    {
        if (string.IsNullOrEmpty(SvnExe.Value))
            Assert.Inconclusive();

    }

    public static string TestHelper(DiffChunk[] f)
    {
        StringBuilder ret = new StringBuilder();
        foreach (var r in f)
        {
            if (r.Type == DifferenceType.None)
                continue;

            ret.Append(r.Original);
            ret.Append("->");
            ret.Append(r.Modified);
            ret.Append(' ');
        }
        return (ret.ToString().Trim());
    }
}
