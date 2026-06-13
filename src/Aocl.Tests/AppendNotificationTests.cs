using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aocl.Tests
{
  [TestClass]
  public class AppendNotificationTests
  {
    // -----------------------------------------------------------------------------------------
    // WaitForAppendAsync (the "doorbell")
    // -----------------------------------------------------------------------------------------

    [TestMethod]
    public async Task WaitForAppendAsync_CompletesWhenItemAppended()
    {
      var sut = new AppendOnlyList<int>();

      var wait = sut.WaitForAppendAsync();
      Assert.IsFalse(wait.IsCompleted, "Should not complete before an append.");

      sut.Append(42);

      await wait.WaitAsync(TimeSpan.FromSeconds(5));
      Assert.IsTrue(wait.IsCompleted);
    }

    [TestMethod]
    public async Task WaitForAppendAsync_AppendWithNoWaiter_DoesNotBreakLaterWait()
    {
      var sut = new AppendOnlyList<int>();

      // No one is waiting: SignalAppend exchanges a null field (the no-allocation fast path).
      sut.Append(1);

      var wait = sut.WaitForAppendAsync();
      Assert.IsFalse(wait.IsCompleted);

      sut.Append(2);
      await wait.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task WaitForAppendAsync_SharedAmongWaiters_AllWakeOnSingleAppend()
    {
      var sut = new AppendOnlyList<int>();

      var a = sut.WaitForAppendAsync();
      var b = sut.WaitForAppendAsync();

      sut.Append(1);

      await Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task WaitForAppendAsync_Cancellation_Throws()
    {
      var sut = new AppendOnlyList<int>();
      using var cts = new CancellationTokenSource();

      var wait = sut.WaitForAppendAsync(cts.Token);
      cts.Cancel();

      try
      {
        await wait.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Fail("Expected the wait to be cancelled.");
      }
      catch (OperationCanceledException)
      {
        // expected
      }
    }

    // -----------------------------------------------------------------------------------------
    // Tail (IAsyncEnumerable adapter)
    // -----------------------------------------------------------------------------------------

    [TestMethod]
    public async Task Tail_YieldsExistingThenAppended()
    {
      var sut = new AppendOnlyList<int>(new[] { 0, 1, 2 });
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      var received = new List<int>();

      var producer = Task.Run(async () =>
      {
        await Task.Delay(50);
        sut.Append(3);
        sut.Append(4);
      });

      await foreach (var item in sut.Tail(0, cts.Token))
      {
        received.Add(item);
        if (received.Count == 5)
        {
          break;
        }
      }

      await producer;
      CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, received);
    }

    [TestMethod]
    public async Task Tail_FromStartIndex_SkipsEarlierElements()
    {
      var sut = new AppendOnlyList<int>(Enumerable.Range(0, 5));
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      var received = new List<int>();

      await foreach (var item in sut.Tail(2, cts.Token))
      {
        received.Add(item);
        if (received.Count == 3)
        {
          break;
        }
      }

      CollectionAssert.AreEqual(new[] { 2, 3, 4 }, received);
    }

    [TestMethod]
    public void Tail_NegativeStartIndex_ThrowsEagerly()
    {
      var sut = new AppendOnlyList<int>();

      // Eager: the bad argument throws on the call, not on the first MoveNextAsync.
      Assert.ThrowsException<ArgumentOutOfRangeException>(() => sut.Tail(-1));
    }

    [TestMethod]
    public async Task Tail_Cancellation_StopsAStreamThatIsCaughtUp()
    {
      var sut = new AppendOnlyList<int>();
      using var cts = new CancellationTokenSource();

      var consumer = Task.Run(async () =>
      {
        await foreach (var _ in sut.Tail(0, cts.Token))
        {
        }
      });

      // Empty list -> the tail catches up immediately and parks in the append wait.
      await Task.Delay(100);
      Assert.IsFalse(consumer.IsCompleted, "A caught-up tail should still be waiting.");

      cts.Cancel();

      try
      {
        await consumer.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Fail("Expected cancellation to stop the stream.");
      }
      catch (OperationCanceledException)
      {
        // expected
      }
    }

    [TestMethod]
    public async Task Tail_ReceivesEveryAppend_NoLostWakeup()
    {
      // The real correctness test: a tail-follower must receive every appended element, in order, with no
      // loss and (critically) no hang. A lost wakeup would leave the consumer parked forever; the timeout
      // token then cancels it and the assertion below fails on a short count.
      const int count = 50_000;
      var sut = new AppendOnlyList<int>();
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
      var received = new List<int>();

      var consumer = Task.Run(async () =>
      {
        await foreach (var item in sut.Tail(0, cts.Token))
        {
          received.Add(item);
          if (received.Count == count)
          {
            break;
          }
        }
      });

      for (var i = 0; i < count; i++)
      {
        sut.Append(i);
      }

      await consumer.WaitAsync(TimeSpan.FromSeconds(30));
      CollectionAssert.AreEqual(Enumerable.Range(0, count).ToList(), received);
    }
  }
}
