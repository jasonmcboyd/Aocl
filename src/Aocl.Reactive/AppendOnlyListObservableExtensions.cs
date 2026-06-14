using System;
using System.Reactive.Linq;

namespace Aocl
{
  /// <summary>
  /// Reactive (IObservable) extensions for <see cref="IAppendOnlyList{T}"/>.
  /// </summary>
  public static class AppendOnlyListObservableExtensions
  {
    /// <summary>
    /// Returns a cold observable that, on each subscription, emits every element from
    /// <paramref name="startIndex"/> onward and then follows the list - pushing new elements as they are
    /// appended, driven by the list's append signal rather than polling. The sequence never completes on its
    /// own (an append-only list has no end); dispose the subscription to stop. Each subscriber follows
    /// independently from its own cursor, so there is no shared multicast buffer - the list itself is the
    /// replay buffer.
    /// </summary>
    /// <remarks>
    /// Scheduling is left to the caller: compose <c>.ObserveOn(scheduler)</c> / <c>.SubscribeOn(scheduler)</c>
    /// as needed. The initial backlog is emitted synchronously on the subscribing thread.
    /// </remarks>
    public static IObservable<T> ToObservable<T>(this IAppendOnlyList<T> source, int startIndex = 0)
    {
      // Validate eagerly, on the ToObservable call rather than at subscription time.
      if (source is null)
      {
        throw new ArgumentNullException(nameof(source));
      }

      if (startIndex < 0)
      {
        throw new ArgumentOutOfRangeException(nameof(startIndex));
      }

      // Observable.Create's cancellation-aware overload gives us the Subscribe/IDisposable/cancellation
      // plumbing; the token is cancelled when the subscriber unsubscribes. All we write is the doorbell loop -
      // the same lost-wakeup-safe register-then-recheck pattern used by the IAsyncEnumerable tail.
      return Observable.Create<T>(async (observer, cancellationToken) =>
      {
        var cursor = startIndex;

        while (true)
        {
          cancellationToken.ThrowIfCancellationRequested();

          // Capture the doorbell before reading Count so an append during the drain wakes this captured task.
          var appended = source.WaitForAppendAsync(cancellationToken);

          var count = source.Count;
          while (cursor < count)
          {
            cancellationToken.ThrowIfCancellationRequested();
            observer.OnNext(source[cursor]);
            cursor++;
          }

          await appended.ConfigureAwait(false);
        }
      });
    }
  }
}
