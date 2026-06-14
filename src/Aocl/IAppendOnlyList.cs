using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Aocl
{
  public interface IAppendOnlyList<T> : IEnumerable<T>, IEnumerable
  {
    /// <summary>
    /// Appends an object to the end of the <see cref="IAppendOnlyList{T}"/>.
    /// </summary>
    /// <param name="value">
    /// The object to append.
    /// </param>
    void Append(T value);

    /// <summary>
    /// Appends a collection of objects to the <see cref="IAppendOnlyList{T}"/>.
    /// </summary>
    /// <param name="values">
    /// The collection of objects to append.
    /// </param>
    void AppendRange(IEnumerable<T> values);

    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    T this[int index] { get; }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="IAppendOnlyList{T}"/>.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Returns an enumerable that iterates through the collection starting at the specified index.
    /// </summary>
    /// <param name="startIndex">The index the enumerable starts at.</param>
    IEnumerable<T> GetEnumerable(int startIndex);

    /// <summary>
    /// Returns a task that completes the next time an element is appended, letting a consumer follow the
    /// collection without polling. Capture the task before re-checking <see cref="Count"/> to avoid a lost
    /// wakeup. The task is a signal only; it carries no appended value.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait for this caller only.</param>
    Task WaitForAppendAsync(CancellationToken cancellationToken = default);
  }
}
