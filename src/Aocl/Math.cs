namespace Aocl
{
  internal static class Math
  {
    /// <summary>
    /// Lookup table mapping the De Bruijn hash to a bit position. Paired with the
    /// 0x07C4ACDD multiplier in <see cref="FastIntegerLog2"/>; the two are a matched
    /// set and must not be changed independently.
    /// </summary>
    private static readonly int[] DeBruijnLog2 =
    {
       0,  9,  1, 10, 13, 21,  2, 29, 11, 14, 16, 18, 22, 25,  3, 30,
       8, 12, 20, 28, 15, 17, 24,  7, 19, 27, 23,  6, 26,  5,  4, 31
    };

    /// <summary>
    /// Returns the floor of the base-2 logarithm of <paramref name="value"/>, i.e. the
    /// zero-based index of its most significant set bit.
    /// </summary>
    /// <remarks>
    /// Defined for positive values only. The bit-smear (the cascade of shift-ORs) fills
    /// every bit below the most significant set bit, collapsing every value in a band
    /// [2^k, 2^(k+1)-1] to 2^(k+1)-1; multiplying by the De Bruijn constant and taking
    /// the top five bits then yields a unique table index per band. This is pure integer
    /// arithmetic, so the result is exact and identical on every runtime (unlike a
    /// floating-point Math.Log approach).
    ///
    /// By construction this returns 0 for an input of 0 and 31 for negative inputs;
    /// callers must not rely on those results.
    ///
    /// Reference: Sean Eron Anderson, "Bit Twiddling Hacks" -
    /// https://graphics.stanford.edu/~seander/bithacks.html#IntegerLogDeBruijn
    /// </remarks>
    internal static int FastIntegerLog2(int value)
    {
      var v = (uint)value;

      v |= v >> 1;
      v |= v >> 2;
      v |= v >> 4;
      v |= v >> 8;
      v |= v >> 16;

      return DeBruijnLog2[(v * 0x07C4ACDDu) >> 27];
    }
  }
}
