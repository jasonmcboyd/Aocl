using System.Collections.Generic;

namespace Aocl
{
    public static class Extensions
    {
        /// <summary>
        /// Creates a <see cref="AppendOnlyList{T}"/> from an <see cref="IEnumerable{T}"/>.
        /// </summary>
        /// <param name="source">
        /// The <see cref="IEnumerable{T}"/> to create an <see cref="AppendOnlyList{T}"/> from.
        /// </param>
        public static AppendOnlyList<T> ToAppendOnlyList<T>(this IEnumerable<T> source) => new AppendOnlyList<T>(source);

        /// <summary>
        /// Creates a <see cref="AppendOnlyList{T}"/> from an <see cref="IEnumerable{T}"/>.
        /// </summary>
        /// <param name="source">
        /// The <see cref="IEnumerable{T}"/> to create an <see cref="AppendOnlyList{T}"/> from.
        /// </param>
        /// <param name="initialBitness">
        /// The initial bitness of the <see cref="AppendOnlyList{T}"/>.
        /// </param>
        public static AppendOnlyList<T> ToAppendOnlyList<T>(this IEnumerable<T> source, int initialBitness) => new AppendOnlyList<T>(source, initialBitness);
        
        /// <summary>
        /// Returns an <see cref="IReadOnlyList{T}"/> wrapper around an <see cref="IAppendOnlyList{T}"./>
        /// </summary>
        /// <param name="source">
        /// The <see cref="IAppendOnlyList{T}"/> wrap as an <see cref="IReadOnlyList{T}"/>.
        /// </param>
        public static IReadOnlyList<T> AsReadOnlyList<T>(this IAppendOnlyList<T> source) => new ReadOnlyWrapper<T>(source);
    }
}
