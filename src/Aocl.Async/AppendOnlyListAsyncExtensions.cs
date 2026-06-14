using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Aocl
{
  /// <summary>
  /// Async streaming extensions for <see cref="IAppendOnlyList{T}"/>.
  /// </summary>
  public static class AppendOnlyListAsyncExtensions
  {
    /// <summary>
    /// Reads the list from <paramref name="startIndex"/> and then keeps reading: yields every element already
    /// present, then awaits new appends - via the list's append signal, with no polling - and yields them as
    /// they arrive. Like a channel's ReadAllAsync, the sequence does not complete on its own (an append-only
    /// list has no end); stop it by cancelling <paramref name="cancellationToken"/>.
    /// </summary>
    /// <remarks>
    /// Pass the token to this method directly (e.g. <c>source.ReadAllAsync(0, ct)</c>); the iterator uses it
    /// both to wake from the append wait and to stop. Each enumerator keeps its own cursor and reads straight
    /// from the list's lock-free indexer, so multiple consumers can read the same list concurrently with no
    /// shared buffering and no impact on writers.
    ///
    /// Once a reader has caught up it is suspended inside the append wait, so the way to stop it is to cancel
    /// the token - breaking out of the enumeration only takes effect while elements are being yielded.
    /// </remarks>
    public static IAsyncEnumerable<T> ReadAllAsync<T>(
      this IAppendOnlyList<T> source,
      int startIndex = 0,
      CancellationToken cancellationToken = default)
    {
      // Validate eagerly so a bad argument throws at the call site, not deferred to the first MoveNextAsync
      // (same reasoning as AppendOnlyList.GetEnumerable's wrapper).
      if (source is null)
      {
        throw new ArgumentNullException(nameof(source));
      }

      if (startIndex < 0)
      {
        throw new ArgumentOutOfRangeException(nameof(startIndex));
      }

      return ReadAllAsyncIterator(source, startIndex, cancellationToken);
    }

    private static async IAsyncEnumerable<T> ReadAllAsyncIterator<T>(
      IAppendOnlyList<T> source,
      int startIndex,
      [EnumeratorCancellation] CancellationToken cancellationToken)
    {
      var cursor = startIndex;

      while (true)
      {
        cancellationToken.ThrowIfCancellationRequested();

        // Register the doorbell BEFORE reading Count. An append that lands while we drain then completes this
        // captured task rather than slipping past us - the lost-wakeup-safe register-then-recheck pattern.
        var appended = source.WaitForAppendAsync(cancellationToken);

        var count = source.Count;
        while (cursor < count)
        {
          yield return source[cursor];
          cursor++;
        }

        // Caught up. Wait for the next append. If items arrived during the drain above, 'appended' is already
        // completed, so this returns immediately and we loop to drain them.
        await appended.ConfigureAwait(false);
      }
    }
  }
}
