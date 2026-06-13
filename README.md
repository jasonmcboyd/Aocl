# Aocl — Append-Only Concurrent List

[![NuGet](https://img.shields.io/nuget/v/Aocl.svg)](https://www.nuget.org/packages/Aocl/)

| | Build | NuGet |
| --- | --- | --- |
| develop | [![Build](https://github.com/jasonmcboyd/Aocl/actions/workflows/build.yml/badge.svg?branch=develop)](https://github.com/jasonmcboyd/Aocl/actions/workflows/build.yml) | |
| master | [![Build](https://github.com/jasonmcboyd/Aocl/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/jasonmcboyd/Aocl/actions/workflows/build.yml) | [![Publish](https://github.com/jasonmcboyd/Aocl/actions/workflows/nuget-publish.yml/badge.svg?branch=master)](https://github.com/jasonmcboyd/Aocl/actions/workflows/nuget-publish.yml) |

`AppendOnlyList<T>` is a generic collection for scenarios where many threads read concurrently while one or more threads append. Reads (by index, `Count`, and enumeration) are **lock-free** — they take no lock and are never blocked by a concurrent writer. Writes are serialized internally, so any number of threads may call `Append` or `AppendRange` safely without external synchronization. The collection targets **netstandard2.0** and has no dependencies.

Typical use cases include in-process event logs, append-only domain event stores, producer/consumer pipelines where consumers need random access to historical entries, and any structure where immutability of already-written entries is an invariant.

## Installation

**.NET CLI**

```
dotnet add package Aocl
```

**Package Manager Console**

```
PM> Install-Package Aocl
```

## Quick Start

```csharp
using Aocl;

// Construct empty
var list = new AppendOnlyList<string>();

// Append single elements
list.Append("alpha");
list.Append("beta");

// Append a range
list.AppendRange(new[] { "gamma", "delta", "epsilon" });

// Random access by index — O(1), lock-free
Console.WriteLine(list[0]);        // alpha
Console.WriteLine(list.Count);     // 5

// foreach — enumerator picks up items appended while iterating
foreach (var item in list)
{
    Console.WriteLine(item);
}

// Catch-up read from a known position — useful for consumers that
// track their own read cursor and need to process only new entries
IEnumerable<string> newItems = list.GetEnumerable(startIndex: 3);
foreach (var item in newItems)
{
    Console.WriteLine(item);       // delta, epsilon
}

// Construct from an existing collection
var seeded = new AppendOnlyList<int>(Enumerable.Range(0, 100));

// LINQ extension method
var fromLinq = Enumerable.Range(0, 100).ToAppendOnlyList();

// Expose a read-only view to consumers that should not append
IReadOnlyList<string> readOnly = list.AsReadOnlyList();
```

## Thread Safety

Thread safety is the primary design goal of this library. The guarantees are:

**Reads are lock-free.**
`Count`, the indexer (`list[i]`), and both enumeration paths (`foreach` / `GetEnumerable`) never acquire a lock. Any number of threads may read concurrently, and reads are safe to interleave with concurrent appends.

**Visibility is guaranteed.**
`Count` is published with release/acquire memory semantics. A thread that observes `Count == N` is guaranteed to see correct, fully-written values at every index in `[0, N)`. This holds on weakly-ordered architectures such as ARM64 — there are no torn or partial reads.

**Writes are serialized.**
`Append` and `AppendRange` acquire an internal lock. Multiple threads may call them concurrently; they will not corrupt the collection, but they do contend with each other.

**Elements are immutable once appended.**
No element is ever moved, overwritten, or removed. This is the property that makes lock-free indexed reads safe: an element's address in memory never changes after it is published.

**Enumerators observe concurrent appends.**
An enumerator started mid-stream will yield items that are appended after the enumerator is created, because it re-checks `Count` on each step. `GetEnumerable(startIndex)` can therefore be used as a lightweight tail/catch-up mechanism.

## Design

`AppendOnlyList<T>` is backed by a fixed jagged array of partitions. Each partition is a plain array allocated once and never resized, moved, or copied. New elements are written into the current partition's next free slot; when a partition fills, a new one is added to the pre-allocated outer array. Because existing partitions and their contents never move, readers can index into them without a lock.

The mapping from a flat index to `(partition, offset)` is O(1): it requires a single `floor(log2)` integer operation. Partition sizes follow a power-of-two scheme — the first two partitions share the initial size, then each subsequent partition doubles — so the number of partitions grows logarithmically with the number of elements.

The sole cross-thread synchronization point is `Count`: it is written with `Volatile.Write` as the very last step of every append, and read with `Volatile.Read`. This release/acquire pair orders all preceding data writes before the count increment becomes visible to readers.

## Tuning — `bitness`

The `bitness` parameter controls the size of the first partition: `size = 2^bitness`. The default is `4`, which gives a first-partition size of 16 elements.

```csharp
// Default: first partition holds 16 elements
var list = new AppendOnlyList<int>();

// bitness = 10: first partition holds 1 024 elements
// Suitable for lists expected to hold thousands of entries,
// reducing the total number of partition allocations.
var large = new AppendOnlyList<int>(bitness: 10);

// Also accepted by the collection constructor and extension method
var seeded = new AppendOnlyList<int>(Enumerable.Range(0, 10_000), bitness: 10);
var fromLinq = Enumerable.Range(0, 10_000).ToAppendOnlyList(initialBitness: 10);
```

`bitness` must be in the range `[1, 30]` inclusive; values outside this range throw `ArgumentOutOfRangeException`. A larger `bitness` means fewer, larger partitions (fewer allocations for big lists). A smaller `bitness` means more, smaller partitions (lower up-front allocation cost for small or short-lived lists).

## API Reference

### `AppendOnlyList<T>`

| Member | Description |
|---|---|
| `AppendOnlyList()` | Creates an empty list with default `bitness` (4). |
| `AppendOnlyList(IEnumerable<T> collection)` | Creates a list pre-populated from `collection`. |
| `AppendOnlyList(int bitness)` | Creates an empty list with the specified `bitness`. |
| `AppendOnlyList(IEnumerable<T> collection, int bitness = 4)` | Creates a pre-populated list with the specified `bitness`. |
| `void Append(T value)` | Appends a single element. Thread-safe; serialized with other writers. |
| `void AppendRange(IEnumerable<T> values)` | Appends all elements from `values`. Thread-safe; serialized with other writers. |
| `T this[int index]` | Returns the element at `index`. Lock-free. Throws `ArgumentOutOfRangeException` if `index < 0` or `index >= Count`. |
| `int Count` | Returns the number of elements. Lock-free; published with acquire semantics. |
| `IEnumerable<T> GetEnumerable(int startIndex)` | Returns an enumerable starting at `startIndex`. Lock-free. Throws `ArgumentOutOfRangeException` if `startIndex < 0`; returns empty if `startIndex >= Count`. Observes elements appended during enumeration. |
| `IEnumerator<T> GetEnumerator()` | Equivalent to `GetEnumerable(0).GetEnumerator()`. |

### Extension Methods (`Aocl.Extensions`)

| Member | Description |
|---|---|
| `IEnumerable<T>.ToAppendOnlyList<T>()` | Creates an `AppendOnlyList<T>` from any `IEnumerable<T>`. |
| `IEnumerable<T>.ToAppendOnlyList<T>(int initialBitness)` | Creates an `AppendOnlyList<T>` with the specified `bitness`. |
| `IAppendOnlyList<T>.AsReadOnlyList<T>()` | Returns an `IReadOnlyList<T>` wrapper. The wrapper reflects appends made to the underlying list. |

## License

MIT
