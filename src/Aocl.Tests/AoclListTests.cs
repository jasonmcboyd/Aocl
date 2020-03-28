using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aocl.Tests
{
    [TestClass]
    public class AoclListTests
    {
        public static IEnumerable<object[]> GetData()
        {
            var bitness = Enumerable.Range(1, 8);
            var count = new int[]
            {
                0,
                1,
                2,
                3,
                4,
                5,
                8,
                9,
                16,
                17,
                32,
                33,
                64,
                65,
                128,
                129,
                256,
                257,
                512,
                513,
                1024,
                1025
            };

            foreach (var b in bitness)
            {
                foreach (var c in count)
                {
                    yield return new object[]
                    {
                        b,
                        Enumerable.Range(0, c).ToList()
                    };
                }
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetData), DynamicDataSourceType.Method)]
        public void AppendRange_CorrectValuesReturned(int bitness, List<int> data)
        {
            // Arrange
            var sut = new AppendOnlyList<int>(bitness);

            // Act
            sut.AppendRange(data);

            // Assert
            Assert.AreEqual(data.Count, sut.Count);
            Assert.IsTrue(Enumerable.SequenceEqual(sut, data));
            for (int i = 0; i < data.Count; i++)
            {
                Assert.AreEqual(data[i], sut[i]);
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetData), DynamicDataSourceType.Method)]
        public void ConstructorAppendRange_CorrectValuesReturned(int bitness, List<int> data)
        {
            // Arrange
            var sut = new AppendOnlyList<int>(bitness);

            // Act
            sut.AppendRange(data);

            // Assert
            Assert.AreEqual(data.Count, sut.Count);
            Assert.IsTrue(Enumerable.SequenceEqual(sut, data));
            for (int i = 0; i < data.Count; i++)
            {
                Assert.AreEqual(data[i], sut[i]);
            }
        }

        [TestMethod]
        [DynamicData(nameof(GetData), DynamicDataSourceType.Method)]
        public void Append_CorrectValuesReturned(int bitness, List<int> data)
        {
            // Arrange
            var sut = new AppendOnlyList<int>(bitness);

            // Act
            for (int i = 0; i < data.Count; i++)
            {
                sut.Append(i);
            }

            // Assert
            Assert.AreEqual(data.Count, sut.Count);
            Assert.IsTrue(Enumerable.SequenceEqual(sut, data));
            for (int i = 0; i < data.Count; i++)
            {
                Assert.AreEqual(data[i], sut[i]);
            }
        }

        //[TestMethod]
        //public void AddRange_MoreThanInitialList_CorrectValuesReturned()
        //{
        //    // Arrange
        //    var numbers = Enumerable.Range(0, 3);
        //    var sut = new AppendOnlyList<int>(1);

        //    // Act
        //    sut.AppendRange(numbers);

        //    // Assert
        //    Assert.IsTrue(Enumerable.SequenceEqual(sut, numbers));
        //}
    }
}
