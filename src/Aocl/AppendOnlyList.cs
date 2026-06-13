using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
      if (bitness < 1 || bitness > 30)
      {
        throw new ArgumentOutOfRangeException(nameof(bitness), "Must be between 1 and 30 inclusive.");
      }

      Bitness = bitness;
      NextPartitionBitness = bitness;
      Partitions = new T[MaxPartitionCount][];
      Partitions[0] = new T[BitnessToSize(Bitness)];
      PartitionCount = 1;
      AppendRange(collection);
    }

    /// <summary>
    /// Upper bound on the number of partitions a list can ever need. The worst case is the smallest
    /// allowed bitness (1): its partition sizes 2, 2, 4, 8, ... reach a combined capacity of 2^31 at the
    /// 31st partition - enough to hold the maximum int.MaxValue (2^31 - 1) elements. Any larger bitness
    /// needs fewer partitions, so 31 always suffices. (No single partition exceeds 2^30.)
    /// </summary>
    private const int MaxPartitionCount = 31;

    /// <summary>
    /// Backing field for <see cref="Count"/>. Written only under <see cref="AppendLock"/> with a
    /// Volatile.Write as the final step of an append, and read by lock-free readers with a Volatile.Read
    /// through the <see cref="Count"/> getter. This release/acquire pair is the sole cross-thread
    /// synchronization: the release orders every preceding data write before the count becomes visible,
    /// so a reader that sees an index below Count is guaranteed to see the element at that index.
    /// </summary>
    private int _Count;

    /// <summary>
    /// Gets the number of elements contained in the <see cref="AppendOnlyList{T}"/>.
    /// </summary>
    public int Count => Volatile.Read(ref _Count);

    /// <summary>
    /// Size (in bits) of the first partition.
    /// </summary>
    private int Bitness { get; }

    /// <summary>
    /// Size (in bits) of the next partition to be created.
    /// </summary>
    private int NextPartitionBitness { get; set; }

    /// <summary>
    /// Fixed-size jagged array of partitions that together hold all the objects. Both the outer array
    /// (sized at construction) and each inner partition (a fixed-length array) are never resized, moved,
    /// or copied. Because a published element therefore never changes address, readers can index into the
    /// partitions without locking; they rely solely on the <see cref="Count"/> memory fence to know an
    /// element has been published.
    /// </summary>
    private T[][] Partitions { get; }

    /// <summary>
    /// Number of populated slots in <see cref="Partitions"/>. Writer-only state guarded by
    /// <see cref="AppendLock"/>; readers never consult it because they derive the partition from the index.
    /// </summary>
    private int PartitionCount { get; set; }

    /// <summary>
    /// Next free slot in the current (last populated) partition. Writer-only state guarded by
    /// <see cref="AppendLock"/>; reset to 0 whenever a partition is added.
    /// </summary>
    private int WriteOffset { get; set; }

    /// <summary>
    /// Helper method that returns the current (always the last populated) partition in <see cref="Partitions"/>.
    /// </summary>
    private T[] CurrentPartition => Partitions[PartitionCount - 1];

    /// <summary>
    /// Calculates size from bits. Size is 2^<see cref="bitness"/>.
    /// </summary>
    /// <param name="bitness">
    /// Number of bits. Always at least 1 (the constructor rejects a smaller bitness, and partition
    /// bitnesses only grow from there), so the result never overflows for the values actually used.
    /// </param>
    private int BitnessToSize(int bitness) => 1 << bitness;

    /// <summary>
    /// Returns the partition and offset represented by the index.
    /// </summary>
    /// <param name="index">
    /// The index to be converted to a partition and offset.
    /// </param>
    private PartitionAndOffset IndexToPartitionAndOffset(int index)
    {
      Debug.Assert(index >= 0, "Caller must pass a non-negative index to IndexToPartitionAndOffset.");

      if (index < Partitions[0].Length)
      {
        return new PartitionAndOffset(0, index);
      }

      var mostSignificantBitIndex = Math.FastIntegerLog2(index);

      return new PartitionAndOffset(mostSignificantBitIndex - (Bitness - 1), index - (1 << mostSignificantBitIndex));
    }

    /// <summary>
    /// Lock that serializes writers; readers are lock-free and never take it. A plain Monitor lock,
    /// whose uncontended acquire is a cheap user-mode operation - only writers ever contend, so the
    /// reader/writer distinction of a ReaderWriterLockSlim would be wasted here.
    /// </summary>
    private readonly object AppendLock = new object();

    /// <summary>
    /// Appends an object to the end of the <see cref="AppendOnlyList{T}"/>.
    /// </summary>
    /// <param name="value">
    /// The object to append.
    /// </param>
    public void Append(T value)
    {
      lock (AppendLock)
      {
        var current = CurrentPartition;
        // Move to a fresh partition once the current one is full. Each partition is a fixed-length array,
        // so a published element never moves - the property that makes lock-free indexed reads safe.
        if (WriteOffset == current.Length)
        {
          current = AddPartition();
        }
        current[WriteOffset] = value;
        WriteOffset++;
        // Publish the element (and any partition added above) with release semantics. This volatile write
        // must be the last write of the append so a lock-free reader that observes the new Count is
        // guaranteed to observe the data behind it. See the Count property.
        Volatile.Write(ref _Count, _Count + 1);
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
      lock (AppendLock)
      {
        var current = CurrentPartition;
        foreach (var value in values)
        {
          if (WriteOffset == current.Length)
          {
            current = AddPartition();
          }
          current[WriteOffset] = value;
          WriteOffset++;
          // Release-publish each element as the last write of its iteration (see Append / the Count property).
          Volatile.Write(ref _Count, _Count + 1);
        }
      }
    }

    /// <summary>
    /// Adds a new partition and handles the "bookkeeping" associated with adding a partition.
    /// </summary>
    private T[] AddPartition()
    {
      // Writes into the pre-allocated jagged-array slot; this store precedes the element write and the
      // Volatile.Write of Count in the calling append, so it is published by that same release.
      var partition = new T[BitnessToSize(NextPartitionBitness)];
      Partitions[PartitionCount] = partition;
      PartitionCount++;
      NextPartitionBitness++;
      WriteOffset = 0;
      return partition;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="AppendOnlyList{T}"/>.
    /// </summary>
    public IEnumerator<T> GetEnumerator() => GetEnumerable(0).GetEnumerator();

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="IEnumerable"/>.
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc cref="IAppendOnlyList{T}.GetEnumerable"/>
    public IEnumerable<T> GetEnumerable(int startIndex)
    {
      // Validate eagerly so the exception surfaces at the call site rather than
      // being deferred to the first MoveNext() of the iterator below.
      if (startIndex < 0)
      {
        throw new ArgumentOutOfRangeException(nameof(startIndex));
      }

      return GetEnumerableIterator(startIndex);
    }

    private IEnumerable<T> GetEnumerableIterator(int startIndex)
    {
      if (startIndex >= Count)
      {
        yield break;
      }

      (var partition, var offset) = IndexToPartitionAndOffset(startIndex);

      var index = startIndex;

      while (index < Count)
      {
        yield return Partitions[partition][offset];

        index++;

        offset++;

        if (offset >= Partitions[partition].Length)
        {
          offset = 0;
          partition++;
        }
      }
    }

    /// <inheritdoc cref="IAppendOnlyList{T}.this[int]"/>
    public T this[int index]
    {
      get
      {
        if (index < 0 || index >= Count)
        {
          throw new ArgumentOutOfRangeException(nameof(index));
        }

        var internalIndex = IndexToPartitionAndOffset(index);
        return Partitions[internalIndex.Partition][internalIndex.Offset];
      }
    }
  }
}
