# Aocl.Reactive

Reactive (Rx) extensions for [Aocl](https://www.nuget.org/packages/Aocl/)'s `AppendOnlyList<T>`.

Adds **`ToObservable`**, which exposes an append-only list as an `IObservable<T>`: each subscriber receives the elements already present, then new elements as they are appended — via the list's built-in append signal, with **no polling**. The sequence never completes on its own; dispose the subscription to stop.

It composes with the full [System.Reactive](https://www.nuget.org/packages/System.Reactive/) operator set (`Where`, `Buffer`, `ObserveOn`, `Merge`, …). For pull-based consumption with backpressure, see [Aocl.Async](https://www.nuget.org/packages/Aocl.Async/).

## Install

```
dotnet add package Aocl.Reactive
```

## Usage

```csharp
using Aocl;
using System;

var list = new AppendOnlyList<int>();

using var subscription = list
    .ToObservable()
    .Subscribe(item => Console.WriteLine(item));

list.Append(1);
list.Append(2);
```

Pass `startIndex` to begin from a known cursor. Each subscription follows independently from its own cursor — the list itself is the replay buffer, so there is no shared multicast state.

## License

MIT
