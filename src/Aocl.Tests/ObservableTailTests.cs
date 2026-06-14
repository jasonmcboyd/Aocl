using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aocl.Tests
{
  [TestClass]
  public class ObservableTailTests
  {
    [TestMethod]
    public async Task ToObservable_EmitsExistingThenAppended()
    {
      var sut = new AppendOnlyList<int>(new[] { 0, 1, 2 });

      var producer = Task.Run(async () =>
      {
        await Task.Delay(50);
        sut.Append(3);
        sut.Append(4);
      });

      var result = await sut.ToObservable().Take(5).ToArray().ToTask().WaitAsync(TimeSpan.FromSeconds(10));

      await producer;
      CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, result);
    }

    [TestMethod]
    public async Task ToObservable_FromStartIndex_SkipsEarlierElements()
    {
      var sut = new AppendOnlyList<int>(Enumerable.Range(0, 5));

      var result = await sut.ToObservable(2).Take(3).ToArray().ToTask().WaitAsync(TimeSpan.FromSeconds(10));

      CollectionAssert.AreEqual(new[] { 2, 3, 4 }, result);
    }

    [TestMethod]
    public void ToObservable_NegativeStartIndex_ThrowsEagerly()
    {
      var sut = new AppendOnlyList<int>();

      // Eager: throws on the ToObservable call, not at subscription time.
      Assert.ThrowsException<ArgumentOutOfRangeException>(() => sut.ToObservable(-1));
    }

    [TestMethod]
    public async Task ToObservable_DisposeStopsDelivery()
    {
      var sut = new AppendOnlyList<int>();
      var received = new ConcurrentQueue<int>();
      var firstDelivered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

      var subscription = sut.ToObservable().Subscribe(x =>
      {
        received.Enqueue(x);
        firstDelivered.TrySetResult(true);
      });

      sut.Append(1);
      await firstDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));

      subscription.Dispose();
      sut.Append(2);
      await Task.Delay(150);

      CollectionAssert.AreEqual(new[] { 1 }, received.ToArray());
    }

    [TestMethod]
    public async Task ToObservable_ReceivesEveryAppend_NoLostWakeup()
    {
      const int count = 50_000;
      var sut = new AppendOnlyList<int>();

      // ToTask subscribes immediately, so the observable is parked on the doorbell before we start appending.
      var collector = sut.ToObservable().Take(count).ToArray().ToTask();

      for (var i = 0; i < count; i++)
      {
        sut.Append(i);
      }

      var result = await collector.WaitAsync(TimeSpan.FromSeconds(30));
      CollectionAssert.AreEqual(Enumerable.Range(0, count).ToList(), result);
    }
  }
}
