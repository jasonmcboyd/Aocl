using System;
using System.Linq;

namespace Aocl
{
    public static class Math
    {
        static int[] LogLookup =
            Enumerable
            .Range(0, 1 << 8)
            .Select(x => x == 0 ? int.MinValue : (int)System.Math.Log(x, 2))
            .ToArray();

        public static int FastIntegerLog2(int value)
        {
            if (value >= 1 << 24)
            {
                return LogLookup[value >> 24] + 24;
            }
            else if (value >= 1 << 16)
            {
                return LogLookup[value >> 16] + 16;
            }
            else if (value >= 1 << 8)
            {
                return LogLookup[value >> 8] + 8;
            }
            else
            {
                return LogLookup[value];
            }
        }
    }
}
