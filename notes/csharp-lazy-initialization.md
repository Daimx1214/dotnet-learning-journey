# Lazy Initialization with Lazy<T>

`Lazy<T>` defers creating an expensive object until it is first used. That keeps startup fast and avoids paying for work that may never be needed — a heavy configuration object, a large lookup table, or a service that only some code paths touch.

## Basic Use and Lazy Properties

The factory delegate runs once, on first access to `.Value`, and the result is cached for every later access. A lazy property is the most common shape: the backing field is a `Lazy<T>`, and the property just returns its value.

```csharp
public class ReportService
{
    private readonly Lazy<Dictionary<string, int>> _wordCounts =
        new(() => BuildWordCounts());

    public Dictionary<string, int> WordCounts => _wordCounts.Value;

    private static Dictionary<string, int> BuildWordCounts()
    {
        Console.WriteLine("Building word counts..."); // runs once, on first access
        return new Dictionary<string, int> { ["hello"] = 3, ["world"] = 1 };
    }
}

var service = new ReportService();
Console.WriteLine("Service created, nothing built yet.");
Console.WriteLine(service.WordCounts["hello"]); // 3 — factory runs here, once
Console.WriteLine(service.WordCounts["hello"]); // 3 — cached, factory does not run again
```

## Thread Safety Modes

`LazyThreadSafetyMode` controls what happens when several threads access `.Value` at once. `ExecutionAndPublication` (the default) locks so the factory runs exactly once. `PublicationOnly` lets several threads run the factory in parallel but publishes only the first result — useful when the factory is cheap and lock-free matters, but wasteful if it is expensive. `None` offers no safety at all and is only safe when you know a single thread will touch it.

```csharp
var lazy = new Lazy<int>(() => 42, LazyThreadSafetyMode.ExecutionAndPublication);
Console.WriteLine(lazy.Value); // 42
```

## When Lazy Hurts

Laziness hides cost: the first request that touches the value pays the full construction time, which can look like a random slowdown in production. If the factory throws, the exception is cached and every later access to `.Value` rethrows the same exception — the factory is not retried. And `Lazy<T>` adds a small allocation and indirection of its own, so do not wrap values that are cheap to create. Prefer eager construction when the object is always needed, or when startup cost is acceptable and predictable latency matters more.

## Key Takeaways

- `Lazy<T>` runs its factory once, on first access to `.Value`, and caches the result.
- The default mode, `ExecutionAndPublication`, guarantees the factory runs exactly once even under concurrency.
- `PublicationOnly` may run the factory on several threads and keep only one result; `None` is single-thread only.
- A factory exception is cached — later accesses rethrow it instead of retrying.
- Avoid lazy for always-needed or cheap objects: it hides first-use latency and adds indirection for no gain.
