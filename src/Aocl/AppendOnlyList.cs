using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aocl
{
    /// <summary>
    /// Represents a strongly typed collection of objects with lock-free reads by index or enumeration, and thread-safe append-only writes.
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
        /// The collection whose elements are copied to the new <see cref="AppendOnlyList{T}"/>.
        /// </param>
        public AppendOnlyList(IEnumerable<T> collection) : this(collection, 4) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AppendOnlyList{T}"/> class that is empty and that has an initial capacity of 2^<paramref name="bitness"/>.
        /// </summary>
        /// <param name="bitness">
        /// Calculates the initial capacity of the <see cref="AppendOnlyList{T}"/>. Initial capacity is 2^<paramref name="bitness"/>.
        /// </param>
        public AppendOnlyList(int bitness) : this(Enumerable.Empty<T>(), bitness) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AppendOnlyList{T}"/> class contains elements copied from <paramref name="collection"/> and that has an initial capacity of 2^<paramref name="bitness"/>.
        /// </summary>
        /// <param name="collection">
        /// The collection whose elements are copied to the new <see cref="AppendOnlyList{T}"/>.
        /// </param>
        /// <param name="bitness">
        /// Calculates the initial capacity of the <see cref="AppendOnlyList{T}"/>. Initial capacity is 2^<paramref name="bitness"/>.
        /// </param>
        public AppendOnlyList(IEnumerable<T> collection, int bitness = 4)
        {
            if (bitness < 1)
            {
                throw new ArgumentOutOfRangeException("Must be greater than zero.", nameof(bitness));
            }

            Bitness = bitness;
            NextPartitionBitness = bitness;
            var capacity = 32 - bitness + 1;
            Partitions = new List<List<T>>(capacity)
            {
                new List<T>(BitnessToSize(Bitness))
            };
            AppendRange(collection);
        }

        /// <summary>
        /// Size (in bits) of the first partition.
        /// </summary>
        private int Bitness { get; }

        /// <summary>
        /// Size (in bits) of the next partition to be created.
        /// </summary>
        private int NextPartitionBitness { get; set; }

        /// <summary>
        /// Calcualtes size from bits. Size is 2^<see cref="bitness"/>.
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
        /// Returns the logical internal index represented by the virtual index. The logical index is a <see cref="ValueTuple{T1, T2}"/> representing a partition and offset.
        /// </summary>
        /// <param name="index">
        /// The virtual index to be converted to a logical index.
        /// </param>
        private (int Partition, int Offset) VirtualIndexToInternalIndex(int index)
        {
            if (index < Partitions[0].Count)
            {
                return (0, index);
            }
            var mostSignificantBitIndex = (int)Math.Log(index, 2);
            return (mostSignificantBitIndex - (Bitness - 1), index - (1 << mostSignificantBitIndex));
        }

        /// <summary>
        /// Collection of collections that hold all the objects. By only appending new collections rather than copying objects to a new collection when the old collection is outgrown I am able to offer lock-free reads.
        /// </summary>
        private List<List<T>> Partitions { get; }

        /// <summary>
        /// Helper method that returns the current (always the last) collection in <see cref="Partitions"/>.
        /// </summary>
        private List<T> CurrentPartition => Partitions[Partitions.Count - 1];

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
                var current = CurrentPartition;
                if (current.Count == current.Capacity)
                {
                    AddPartition();
                    current = CurrentPartition;
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
                var current = CurrentPartition;
                foreach (var value in values)
                {
                    if (current.Count == current.Capacity)
                    {
                        AddPartition();
                        current = CurrentPartition;
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
        /// Adds a new partition and handles the "bookkeeping" associated with adding a partition.
        /// </summary>
        private void AddPartition()
        {
            var partition = new List<T>(BitnessToSize(NextPartitionBitness));
            Partitions.Add(partition);
            NextPartitionBitness++;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="AppendOnlyList{T}"/>.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            var internalIndex = VirtualIndexToInternalIndex(Count - 1);
            for (int i = 0; i < internalIndex.Item1; i++)
            {
                for (int j = 0; j < Partitions[i].Count; j++)
                {
                    yield return Partitions[i][j];
                }
            }
            for (int j = 0; j <= internalIndex.Item2; j++)
            {
                yield return Partitions[internalIndex.Item1][j];
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
                return Partitions[internalIndex.Partition][internalIndex.Offset];
            }
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="AppendOnlyList{T}"/>.
        /// </summary>
        public int Count { get; private set; }
    }
}
