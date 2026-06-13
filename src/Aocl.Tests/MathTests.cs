using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aocl.Tests
{
  [TestClass]
  public class MathTests
  {
    [TestMethod]
    public void FastIntegerLog2_PowersOfTwo_ReturnExactExponent()
    {
      for (var k = 0; k <= 30; k++)
      {
        Assert.AreEqual(k, Aocl.Math.FastIntegerLog2(1 << k), $"2^{k}");
      }
    }

    [TestMethod]
    public void FastIntegerLog2_BandBoundaries_ReturnFloorLog2()
    {
      // The top of each band [2^k, 2^(k+1)-1] must still report k - this is the
      // off-by-one the old floating-point table was at risk of getting wrong.
      for (var k = 0; k <= 29; k++)
      {
        var upper = (1 << (k + 1)) - 1;
        Assert.AreEqual(k, Aocl.Math.FastIntegerLog2(upper), $"2^{k + 1} - 1 = {upper}");
      }

      // The highest band tops out at int.MaxValue (2^31 - 1).
      Assert.AreEqual(30, Aocl.Math.FastIntegerLog2(int.MaxValue));
    }

    [TestMethod]
    [Ignore("Exhaustive proof over all 2^32 inputs (~50s). Run manually to re-verify FastIntegerLog2.")]
    public void FastIntegerLog2_Exhaustive_MatchesIntegerOracle()
    {
      // Every uint is verified against an independent integer oracle. Casting uint to
      // int round-trips inside FastIntegerLog2 (it casts straight back to uint), so this
      // also covers the negative-int half of the domain. v = 0 is skipped: log2(0) is
      // undefined and the production indexer never passes a non-positive value.
      for (uint v = 1; ; v++)
      {
        var expected = IntegerLog2Oracle(v);
        var actual = Aocl.Math.FastIntegerLog2((int)v);

        if (actual != expected)
        {
          Assert.Fail($"FastIntegerLog2({v}) = {actual}, expected {expected}.");
        }

        if (v == uint.MaxValue) { break; }
      }
    }

    private static int IntegerLog2Oracle(uint v)
    {
      var log = 0;
      while ((v >>= 1) != 0) { log++; }
      return log;
    }
  }
}
