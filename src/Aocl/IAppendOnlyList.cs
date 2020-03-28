using System.Collections;
using System.Collections.Generic;

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

        T this[int index] { get; }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="IAppendOnlyList{T}"/>.
        /// </summary>
        int Count { get; }
    }
}
