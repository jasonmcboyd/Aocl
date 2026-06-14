# Aocl.Async

Async streaming extensions for [Aocl](https://www.nuget.org/packages/Aocl/)'s `AppendOnlyList<T>`.

Adds **`ReadAllAsync`**, which follows an append-only list as an `IAsyncEnumerable<T>`: it yields the elements already present, then awaits new appends — via the list's built-in append signal, with **no polling** — and yields them as they arrive. Like a channel's `ReadAllAsync`, it does not complete on its own; stop it by cancelling the token.

`ReadAllAsync` gives natural backpressure (the consumer pulls the next item only when it's ready), which makes it a good fit for async per-item processing. For push-based / Rx composition, see [Aocl.Reactive](https://www.nuget.org/packages/Aocl.Reactive/).

## Install

```
dotnet add package Aocl.Async
```

## Usage

```csharp
using Aocl;

var list = new AppendOnlyList<int>();

// Follow the list from the beginning, processing each entry as it is appended.
// Runs until the token is cancelled.
await foreach (var item in list.ReadAllAsync(startIndex: 0, cancellationToken))
{
    await ProcessAsync(item);
}
```

Pass `startIndex` to resume from a known cursor (a catch-up read). Each consumer keeps its own cursor and reads straight from the list's lock-free indexer, so multiple consumers can follow the same list concurrently.

## License

MIT
