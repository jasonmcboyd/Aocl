using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aocl.Tests
{
    [TestClass]
    public class MathTests
    {
        public static IEnumerable<object[]> GetData() => Enumerable.Range(1, 128).Select(x => new object[] { x });

        [TestMethod]
        [DynamicData(nameof(GetData), DynamicDataSourceType.Method)]
        public void FastIntegerLog2_ReturnsSameValuesAsMathLog(int value)
        {
            // Arrange
            // Act
            var fastLog = Aocl.Math.FastIntegerLog2(value);
            var systemLog = (int)System.Math.Log(value, 2);

            // Assert
            Assert.AreEqual(systemLog, fastLog);
        }
    }
}
