using System.Collections;
using System.Collections.Generic;

namespace Aocl
{
  class ReadOnlyWrapper<T> : IReadOnlyList<T>
  {
    public ReadOnlyWrapper(IAppendOnlyList<T> inner)
    {
      Inner = inner ?? new AppendOnlyList<T>(1);
    }

    private IAppendOnlyList<T> Inner { get; }

    public T this[int index] => Inner[index];

    public int Count => Inner.Count;

    public IEnumerator<T> GetEnumerator() => Inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  }
}
