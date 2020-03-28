using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aocl
{
    /// <summary>
    /// Represents a strongly typed list of objects with lock-free reads by index or enumeration, and thread-safe append-only writes.
    /// </summary>
    public class AppendOnlyList<T> : IAppendOnlyList<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AppendOnlyList{T}"/> class that is empty and has the default initial capacity.
        /// </summary>
        public AppendOnlyList() : this(Enumerable.Empty<T>(), 4) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AppendOnlyList{T}"/> class contains elements copied from <paramref name="collection"/>.
        /// </summary>
        /// <param name="collection">
        /// The collection whose elements are copied to the new list.
        /// </param>
        public AppendOnlyList(IEnumerable<T> collection) : this(collection, 4) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AppendOnlyList{T}"/> class that is empty and that has an initial capacity of 2^<paramref name="initialBitness"/>.
        /// </summary>
        /// <param name="initialBitness">
        /// Use to calculate the initial capacity of the list. Initial capacity is 2^<paramref name="initialBitness"/>.
        /// </param>
        public AppendOnlyList(int initialBitness) : this(Enumerable.Empty<T>(), initialBitness) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AppendOnlyList{T}"/> class contains elements copied from <paramref name="collection"/> and that has an initial capacity of 2^<paramref name="initialBitness"/>.
        /// </summary>
        /// <param name="collection">
        /// The collection whose elements are copied to the new list.
        /// </param>
        /// <param name="initialBitness">
        /// Use to calculate the initial capacity of the list. Initial capacity is 2^<paramref name="initialBitness"/>.
        /// </param>
        public AppendOnlyList(IEnumerable<T> collection, int initialBitness = 4)
        {
            if (initialBitness < 1)
            {
                throw new ArgumentOutOfRangeException("Must be greater than zero.", nameof(initialBitness));
            }

            InitialListBitness = initialBitness;
            NextListBitness = initialBitness;
            InternalLists = new List<List<T>>
            {
                new List<T>(BitnessToSize(InitialListBitness))
            };
            AppendRange(collection);
        }

        /// <summary>
        /// Size (in bits) of the first list.
        /// </summary>
        private int InitialListBitness { get; }

        /// <summary>
        /// Size (in bits) of the next list to be created.
        /// </summary>
        private int NextListBitness { get; set; }

        /// <summary>
        /// Calcualtes size from bits. Size is 2 ^ <see cref="bitness"/>.
        /// </summary>
        /// <param name="bitness">
        /// Number of bits.
        /// </param>
        private int BitnessToSize(int bitness)
        {
            if (bitness == 0)
            {
                return 0;
            }
            return 1 << bitness;
        }

        /// <summary>
        /// Returns the logical internal index represented by the virtual index. The logical index is a <see cref="ValueTuple{T1, T2}"/> representing a list and position in that list.
        /// </summary>
        /// <param name="index">
        /// The virtual index to be converted to a logical index.
        /// </param>
        private (int, int) VirtualIndexToInternalIndex(int index)
        {
            if (index < InternalLists[0].Count)
            {
                return (0, index);
            }
            var mostSignificantBitIndex = (int)Math.Log(index, 2);
            return (mostSignificantBitIndex - (InitialListBitness - 1), index - (1 << mostSignificantBitIndex));
        }

        /// <summary>
        /// Collection of lists that hold all the objects. By only appending new lists rather than copying objects to a new list when the old list is outgrown I am able to offer lock-free reads.
        /// </summary>
        private List<List<T>> InternalLists { get; }

        /// <summary>
        /// Helper method that returns the current (always the last) list in <see cref="InternalLists"/>.
        /// </summary>
        private List<T> CurrentInternalList => InternalLists[InternalLists.Count - 1];

        /// <summary>
        /// Lock used to ensure only one writer at a time.
        /// </summary>
        private ReaderWriterLockSlim AppendLock { get; } = new ReaderWriterLockSlim();

        /// <summary>
        /// Appends an object to the end of the <see cref="AppendOnlyList{T}"/>.
        /// </summary>
        /// <param name="value">
        /// The object to append.
        /// </param>
        public void Append(T value)
        {
            AppendLock.EnterWriteLock();
            try
            {
                var current = CurrentInternalList;
                if (current.Count == current.Capacity)
                {
                    AddInternalList();
                    current = CurrentInternalList;
                }
                current.Add(value);
                Count++;
            }
            finally
            {
                AppendLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Appends a collection of objects to the <see cref="AppendOnlyList{T}"/>.
        /// </summary>
        /// <param name="values">
        /// The collection of objects to append.
        /// </param>
        public void AppendRange(IEnumerable<T> values)
        {
            AppendLock.EnterWriteLock();
            try
            {
                var current = CurrentInternalList;
                foreach (var value in values)
                {
                    if (current.Count == current.Capacity)
                    {
                        AddInternalList();
                        current = CurrentInternalList;
                    }
                    current.Add(value);
                    Count++;
                }
            }
            finally
            {
                AppendLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Adds a new list to the collection of lists.
        /// </summary>
        private void AddInternalList()
        {
            var newList = new List<T>(BitnessToSize(NextListBitness));
            InternalLists.Add(newList);
            NextListBitness++;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="AppendOnlyList{T}"/>.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            var internalIndex = VirtualIndexToInternalIndex(Count - 1);
            for (int i = 0; i < internalIndex.Item1; i++)
            {
                for (int j = 0; j < InternalLists[i].Count; j++)
                {
                    yield return InternalLists[i][j];
                }
            }
            for (int j = 0; j <= internalIndex.Item2; j++)
            {
                yield return InternalLists[internalIndex.Item1][j];
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="IEnumerable"/>.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public T this[int index]
        {
            get
            {
                var count = Count;
                if (index >= count)
                {
                    throw new IndexOutOfRangeException();
                }
                var internalIndex = VirtualIndexToInternalIndex(index);
                return InternalLists[internalIndex.Item1][internalIndex.Item2];
            }
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="AppendOnlyList{T}"/>.
        /// </summary>
        public int Count { get; private set; }
    }
}
