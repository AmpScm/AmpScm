using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Specialized;
using AmpScm.BucketTests.Buckets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BucketTests;

[TestClass]
public class CombineTests
{
    [TestMethod]
    public void VerifyXor()
    {
        var left = Enumerable.Range(0, 300).SelectMany(x => BitConverter.GetBytes(x * 27)).ToArray().AsBucket();
        var right = Enumerable.Range(0, 300).Reverse().SelectMany(x => BitConverter.GetBytes(x * 31)).ToArray().AsBucket();

        var xorResult = new BitwiseXorBucket(left, right).ToArray().SelectPer(4).Select(x => BitConverter.ToInt32(x, 0));


        Assert.IsTrue(xorResult.SequenceEqual(Enumerable.Range(0, 300).Select(x => x * 27).Zip(Enumerable.Range(0, 300).Reverse().Select(x => x * 31), (x, y) => x ^ y)));


        left = Enumerable.Range(0, 600).SelectMany(x => BitConverter.GetBytes(x * 27)).ToArray().AsBucket();
        right = Enumerable.Range(0, 300).Reverse().SelectMany(x => BitConverter.GetBytes(x * 31)).ToArray().AsBucket();

        xorResult = new BitwiseXorBucket(left, right).ToArray().SelectPer(4).Select(x => BitConverter.ToInt32(x, 0));

        Assert.IsTrue(xorResult.SequenceEqual(Enumerable.Range(0, 600).Select(x => x * 27).Zip(Enumerable.Range(0, 300).Reverse().Select(x => x * 31).Concat(Enumerable.Range(0, 300).Select(x => 0)), (x, y) => x ^ y)));


        left = Enumerable.Range(0, 300).SelectMany(x => BitConverter.GetBytes(x * 27)).ToArray().AsBucket();
        right = Enumerable.Range(0, 600).Reverse().SelectMany(x => BitConverter.GetBytes(x * 31)).ToArray().AsBucket();

        xorResult = new BitwiseXorBucket(left, right).ToArray().SelectPer(4).Select(x => BitConverter.ToInt32(x, 0));

        Assert.IsTrue(xorResult.SequenceEqual(Enumerable.Range(0, 300).Select(x => x * 27).Concat(Enumerable.Range(0, 300).Select(x => 0)).Zip(Enumerable.Range(0, 600).Reverse().Select(x => x * 31), (x, y) => x ^ y)));
    }

    [TestMethod]
    public void VerifyAnd()
    {
        var left = Enumerable.Range(0, 300).SelectMany(x => BitConverter.GetBytes(x * 27)).ToArray().AsBucket();
        var right = Enumerable.Range(0, 300).Reverse().SelectMany(x => BitConverter.GetBytes(x * 31)).ToArray().AsBucket();

        var andResult = new BitwiseAndBucket(left, right).ToArray().SelectPer(4).Select(x => BitConverter.ToInt32(x, 0));


        Assert.IsTrue(andResult.SequenceEqual(Enumerable.Range(0, 300).Select(x => x * 27).Zip(Enumerable.Range(0, 300).Reverse().Select(x => x * 31), (x, y) => x & y)));

        left = Enumerable.Range(0, 600).SelectMany(x => BitConverter.GetBytes(x * 27)).ToArray().AsBucket();
        right = Enumerable.Range(0, 300).Reverse().SelectMany(x => BitConverter.GetBytes(x * 31)).ToArray().AsBucket();

        andResult = new BitwiseAndBucket(left, right).ToArray().SelectPer(4).Select(x => BitConverter.ToInt32(x, 0));

        Assert.IsTrue(andResult.SequenceEqual(Enumerable.Range(0, 600).Select(x => x * 27).Zip(Enumerable.Range(0, 300).Reverse().Select(x => x * 31).Concat(Enumerable.Range(0, 300).Select(x => 0)), (x, y) => x & y)));

        left = Enumerable.Range(0, 300).SelectMany(x => BitConverter.GetBytes(x * 27)).ToArray().AsBucket();
        right = Enumerable.Range(0, 600).Reverse().SelectMany(x => BitConverter.GetBytes(x * 31)).ToArray().AsBucket();

        andResult = new BitwiseAndBucket(left, right).ToArray().SelectPer(4).Select(x => BitConverter.ToInt32(x, 0));

        Assert.IsTrue(andResult.SequenceEqual(Enumerable.Range(0, 300).Select(x => x * 27).Concat(Enumerable.Range(0, 300).Select(x => 0)).Zip(Enumerable.Range(0, 600).Reverse().Select(x => x * 31), (x, y) => x & y)));
    }

    [TestMethod]
    public void VerifyOr()
    {
        var left = Enumerable.Range(0, 300).SelectMany(x => BitConverter.GetBytes(x * 27)).ToArray().AsBucket();
        var right = Enumerable.Range(0, 300).Reverse().SelectMany(x => BitConverter.GetBytes(x * 31)).ToArray().AsBucket();

        var orResult = new BitwiseOrBucket(left, right).ToArray().SelectPer(4).Select(x => BitConverter.ToInt32(x, 0));


        Assert.IsTrue(orResult.SequenceEqual(Enumerable.Range(0, 300).Select(x => x * 27).Zip(Enumerable.Range(0, 300).Reverse().Select(x => x * 31), (x, y) => x | y)));

        left = Enumerable.Range(0, 600).SelectMany(x => BitConverter.GetBytes(x * 27)).ToArray().AsBucket();
        right = Enumerable.Range(0, 300).Reverse().SelectMany(x => BitConverter.GetBytes(x * 31)).ToArray().AsBucket();

        orResult = new BitwiseOrBucket(left, right).ToArray().SelectPer(4).Select(x => BitConverter.ToInt32(x, 0));

        Assert.IsTrue(orResult.SequenceEqual(Enumerable.Range(0, 600).Select(x => x * 27).Zip(Enumerable.Range(0, 300).Reverse().Select(x => x * 31).Concat(Enumerable.Range(0, 300).Select(x => 0)), (x, y) => x | y)));

        left = Enumerable.Range(0, 300).SelectMany(x => BitConverter.GetBytes(x * 27)).ToArray().AsBucket();
        right = Enumerable.Range(0, 600).Reverse().SelectMany(x => BitConverter.GetBytes(x * 31)).ToArray().AsBucket();

        orResult = new BitwiseOrBucket(left, right).ToArray().SelectPer(4).Select(x => BitConverter.ToInt32(x, 0));

        Assert.IsTrue(orResult.SequenceEqual(Enumerable.Range(0, 300).Select(x => x * 27).Concat(Enumerable.Range(0, 300).Select(x => 0)).Zip(Enumerable.Range(0, 600).Reverse().Select(x => x * 31), (x, y) => x | y)));
    }

    [TestMethod]
    public void VerifyBitwiseNot()
    {
        var left = Enumerable.Range(0, 300).SelectMany(x => BitConverter.GetBytes(x * 27)).ToArray().AsBucket();

        var notResult = new BitwiseNotBucket(left).ToArray().SelectPer(4).Select(x => BitConverter.ToInt32(x, 0));


        Assert.IsTrue(notResult.SequenceEqual(Enumerable.Range(0, 300).Select(x => ~(x * 27))));
    }

    [TestMethod]
    public async Task TakeSkipTests()
    {
        var bb = await new byte[128].AsBucket().Take(130).ReadExactlyAsync(200);
        Assert.AreEqual(128, bb.Length);

        try
        {
            bb = await new byte[128].AsBucket().TakeExactly(130).ReadExactlyAsync(200);
            Assert.Fail("Expected eof exception");
        }
        catch(BucketEofException)
        {
        }


        bb = await new byte[128].AsBucket().Skip(130).ReadExactlyAsync(200);
        Assert.AreEqual(0, bb.Length);
        Assert.IsTrue(bb.IsEof);
        Assert.IsTrue(bb.IsEmpty);

        try
        {
            bb = await new byte[128].AsBucket().SkipExactly(130).ReadExactlyAsync(200);
            Assert.Fail("Expected eof exception");
        }
        catch (BucketEofException)
        {
        }
    }
}
