using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    [TestMethod]
    public void AddRange_MoreThanInitialCapacity_CorrectValuesReturned()
    {
      // Arrange
      var numbers = Enumerable.Range(0, 3);
      var sut = new AppendOnlyList<int>(1);

      // Act
      sut.AppendRange(numbers);

      // Assert
      Assert.IsTrue(Enumerable.SequenceEqual(sut, numbers));
    }

    [TestMethod]
    public void GetEnumerable_StartIndexLessThanZero_ThrowsArgumentOutOfRangeException()
    {
      // Arrange
      var numbers = Enumerable.Range(0, 3);
      var sut = new AppendOnlyList<int>(numbers);

      // Act / Assert: validation is eager, so the call itself throws without
      // needing to enumerate the result.
      Assert.ThrowsException<ArgumentOutOfRangeException>(() => sut.GetEnumerable(-1));
    }

    [TestMethod]
    public void GetEnumerable_StartIndexGreaterThanCount_EmptyEnumerableReturned()
    {
      // Arrange
      var numbers = Enumerable.Range(0, 3);
      var sut = new AppendOnlyList<int>(numbers);

      // Act
      var enumerable = sut.GetEnumerable(10);

      // Assert
      Assert.IsFalse(enumerable.Any());
    }

    [TestMethod]
    public void GetEnumerator_AddItemMidEnumeration_FullCollectionEnumerated()
    {
      // Arrange
      var numbers = Enumerable.Range(0, 1);
      var sut = new AppendOnlyList<int>(numbers);
      var count = 0;

      // Act
      var enumerator = sut.GetEnumerator();
      if (enumerator.MoveNext())
        count++;

      sut.Append(1);

      while (enumerator.MoveNext())
        count++;

      // Assert
      Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void GetEnumerator_CollectionIsEmpty_AddItemAfterEnumeratorInstantiated_FullCollectionEnumerated()
    {
      // Arrange
      var sut = new AppendOnlyList<int>();
      var count = 0;

      // Act
      var enumerator = sut.GetEnumerator();

      sut.Append(1);

      while (enumerator.MoveNext())
        count++;

      // Assert
      Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void GetEnumerator_ExhaustEnumerator_AddItem_MoveNextReturnsFalse()
    {
      // Arrange
      var sut = new AppendOnlyList<int>();

      // Act
      var enumerator = sut.GetEnumerator();

      while (enumerator.MoveNext()) ;

      sut.Append(1);

      // Assert
      Assert.IsFalse(enumerator.MoveNext());
    }

    [DataTestMethod]
    [DataRow(int.MinValue)]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(31)]  // 1 << 31 overflows int to a negative size
    [DataRow(32)]  // 1 << 32 silently wraps to 1 (C# masks the shift count to 5 bits)
    [DataRow(int.MaxValue)]
    public void Constructor_BitnessOutOfRange_ThrowsArgumentOutOfRangeException(int bitness)
    {
      Assert.ThrowsException<ArgumentOutOfRangeException>(() => new AppendOnlyList<int>(bitness));
    }

    // ---------------------------------------------------------------------------------------------
    // Concurrency stress tests.
    //
    // REGRESSION GUARD, NOT A PROOF. These tests exercise lock-free readers against an active
    // appender to guard against gross regressions of the publish/acquire contract: a reader that
    // observes Count == N must see fully-published, correct elements at every index in [0, N) and
    // must never read a default/garbage value or throw. The known sequence (value == index) makes
    // every read independently verifiable.
    //
    // Note that passing these tests does NOT prove memory-ordering correctness. On x86/x64's strong
    // memory model the underlying race can stay invisible even when the publishing barrier is wrong;
    // the bug primarily bites on weak-ordered architectures such as ARM64. So treat these as a
    // documented contract and a tripwire for obvious breakage, not as evidence of correctness.
    // ---------------------------------------------------------------------------------------------

    // Number of items the appender publishes. Sized for a ~1-3s CI run while still producing many
    // partition transitions (each capacity-doubling boundary is a fresh partition slot the reader
    // must traverse correctly).
    private const int StressItemCount = 250_000;

    // The value stored at index i. Kept trivial (identity) so the reader can verify each element in
    // O(1) without any side table. Pulled into a method to document intent at the assertion sites.
    private static int ExpectedValueFor(int index) => index;

    [TestMethod]
    [Timeout(30_000)]
    public void Stress_ConcurrentReadersDuringAppend_IndexerAlwaysSeesPublishedValues()
    {
      // Arrange
      var sut = new AppendOnlyList<int>(4);
      var readerExceptions = new ConcurrentQueue<Exception>();
      // Latches the highest Count any reader observed so we can confirm readers genuinely raced the
      // appender (saw a moving target) rather than only running after it finished.
      var maxObservedCount = 0;
      using var done = new CountdownEvent(1);

      // The appender: publishes the known sequence value == index for every index.
      var appender = Task.Run(() =>
      {
        try
        {
          for (int i = 0; i < StressItemCount; i++)
          {
            sut.Append(ExpectedValueFor(i));
          }
        }
        finally
        {
          done.Signal();
        }
      });

      // Readers: while the appender runs, repeatedly snapshot Count and verify the prefix [0, n).
      Action reader = () =>
      {
        try
        {
          while (!done.IsSet)
          {
            // Snapshot once; everything below [0, n) must be valid for this snapshot.
            var n = sut.Count;
            InterlockedMax(ref maxObservedCount, n);

            if (n == 0)
            {
              continue;
            }

            // Spot-check the boundary (most likely to expose a half-published tail element) plus a
            // stride sample across the snapshotted prefix. Checking the exact tail index n-1 is the
            // point: it is the element most recently published relative to this Count read.
            Assert.AreEqual(ExpectedValueFor(n - 1), sut[n - 1]);

            var step = System.Math.Max(1, n / 64);
            for (int i = 0; i < n; i += step)
            {
              Assert.AreEqual(ExpectedValueFor(i), sut[i]);
            }
          }

          // Final pass: after the appender has finished, the full sequence must be intact.
          var finalCount = sut.Count;
          for (int i = 0; i < finalCount; i++)
          {
            Assert.AreEqual(ExpectedValueFor(i), sut[i]);
          }
        }
        catch (Exception ex)
        {
          readerExceptions.Enqueue(ex);
        }
      };

      // Act: run several concurrent readers alongside the single appender.
      var readerCount = System.Math.Max(2, Environment.ProcessorCount - 1);
      var readers = Enumerable.Range(0, readerCount)
        .Select(_ => Task.Run(reader))
        .ToArray();

      Task.WaitAll(readers.Append(appender).ToArray());

      // Assert
      Assert.IsTrue(
        readerExceptions.IsEmpty,
        "Reader thread(s) observed inconsistent state: " +
          string.Join(" | ", readerExceptions.Select(e => e.ToString())));
      Assert.AreEqual(StressItemCount, sut.Count);
      Assert.IsTrue(
        maxObservedCount > 0,
        "Readers never observed a non-empty list mid-append; the test did not exercise concurrency.");
    }

    [TestMethod]
    [Timeout(30_000)]
    public void Stress_ConcurrentEnumerationDuringAppend_YieldsContiguousExpectedSequence()
    {
      // Arrange
      var sut = new AppendOnlyList<int>(4);
      var readerExceptions = new ConcurrentQueue<Exception>();
      var maxYielded = 0;
      using var done = new CountdownEvent(1);

      var appender = Task.Run(() =>
      {
        try
        {
          for (int i = 0; i < StressItemCount; i++)
          {
            sut.Append(ExpectedValueFor(i));
          }
        }
        finally
        {
          done.Signal();
        }
      });

      // Readers enumerate from a moving start index and assert the yielded values are exactly the
      // contiguous expected sequence starting at that index. GetEnumerable walks across partition
      // boundaries, so a mis-published partition slot or element would surface as a value mismatch
      // (or an exception) rather than silently passing.
      Action reader = () =>
      {
        try
        {
          var random = new Random(Environment.CurrentManagedThreadId);
          while (!done.IsSet)
          {
            var n = sut.Count;
            if (n == 0)
            {
              continue;
            }

            var start = random.Next(0, n);
            var expected = start;
            foreach (var value in sut.GetEnumerable(start))
            {
              Assert.AreEqual(ExpectedValueFor(expected), value);
              expected++;
            }

            // The enumerator must have yielded a contiguous run from start up to at least the Count
            // snapshot taken before enumeration began (it may yield more if the appender advanced).
            Assert.IsTrue(
              expected >= n,
              $"Enumeration from {start} yielded only up to {expected}; expected at least {n}.");
            InterlockedMax(ref maxYielded, expected);
          }
        }
        catch (Exception ex)
        {
          readerExceptions.Enqueue(ex);
        }
      };

      // Act
      var readerCount = System.Math.Max(2, Environment.ProcessorCount - 1);
      var readers = Enumerable.Range(0, readerCount)
        .Select(_ => Task.Run(reader))
        .ToArray();

      Task.WaitAll(readers.Append(appender).ToArray());

      // Assert
      Assert.IsTrue(
        readerExceptions.IsEmpty,
        "Reader thread(s) observed inconsistent enumeration: " +
          string.Join(" | ", readerExceptions.Select(e => e.ToString())));
      Assert.AreEqual(StressItemCount, sut.Count);
      Assert.IsTrue(
        maxYielded > 0,
        "Readers never enumerated any elements mid-append; the test did not exercise concurrency.");

      // Final full enumeration must be the complete, contiguous sequence.
      Assert.IsTrue(Enumerable.SequenceEqual(sut, Enumerable.Range(0, StressItemCount)));
    }

    // Lock-free monotonic max via compare-exchange. Used only to record observed progress for the
    // "did we actually race?" assertions; it is not part of the contract under test.
    private static void InterlockedMax(ref int target, int value)
    {
      var current = Volatile.Read(ref target);
      while (value > current)
      {
        var observed = Interlocked.CompareExchange(ref target, value, current);
        if (observed == current)
        {
          return;
        }
        current = observed;
      }
    }
  }
}
