# Immutable Collections in C# (C#)

`System.Collections.Immutable` provides collections that never change after creation: every "modifying" operation returns a new collection and leaves the original untouched. They live in the `System.Collections.Immutable` NuGet package (included in the shared framework for .NET Core 3.0+ app models, and available as a package for everything else). Immutability removes an entire class of bugs — a method cannot accidentally mutate a collection its caller still relies on — and it makes sharing data between threads safe without locks.

## The Main Types

| Type | Mutable counterpart | Notes |
|------|--------------------|-------|
| `ImmutableList<T>` | `List<T>` | Ordered, indexable; add/remove are O(log n) |
| `ImmutableArray<T>` | `T[]` | Fixed-size, fastest reads; the default empty value is uninitialized — use `ImmutableArray<T>.Empty` |
| `ImmutableDictionary<K, V>` | `Dictionary<K, V>` | Key lookup; add/remove are O(log n) |
| `ImmutableHashSet<T>` | `HashSet<T>` | Unique values; O(log n) add/remove |
| `ImmutableQueue<T>` | `Queue<T>` | FIFO, backed by two immutable stacks |
| `ImmutableStack<T>` | `Stack<T>` | LIFO, O(1) push/pop |
| `ImmutableSortedSet<T>` | `SortedSet<T>` | Ordered unique values |

Internally these are persistent data structures — usually balanced trees (AVL) or tries — where a new version shares most of its nodes with the old one. That structural sharing is why creating a "modified copy" is O(log n) instead of O(n): only the path from the root to the changed node is rebuilt.

## Basic Usage

Every mutating-style method returns a new instance. Forgetting to capture the return value is the most common beginner mistake — the original collection is unchanged, so the update silently disappears.

```csharp
using System.Collections.Immutable;

ImmutableList<string> playlist = ImmutableList.Create("Song A", "Song B");

// Add returns a NEW list; playlist itself is unchanged.
ImmutableList<string> updated = playlist.Add("Song C");
Console.WriteLine(playlist.Count); // 2
Console.WriteLine(updated.Count);  // 3

ImmutableDictionary<string, int> scores = ImmutableDictionary<string, int>.Empty
    .Add("Ali", 120)
    .Add("Sara", 95);

ImmutableDictionary<string, int> higher = scores.SetItem("Ali", 150);
Console.WriteLine(scores["Ali"]); // 120 — original untouched
Console.WriteLine(higher["Ali"]); // 150
```

`ImmutableArray<T>` deserves a warning: `default(ImmutableArray<int>)` is not an empty array, it is an uninitialized struct whose methods throw `NullReferenceException`. Always create it with `ImmutableArray.Create(...)`, `.ToImmutableArray()`, or `ImmutableArray<T>.Empty`, and check `IsDefault` if a value could come from an uninitialized field.

## The Builder Pattern

Building an immutable collection one `Add` at a time creates a new version per item, which is O(n log n) total and allocates a lot. When you need to build a collection in a loop, use a builder: it is a mutable buffer that produces the immutable collection once, in O(n).

```csharp
var builder = ImmutableList.CreateBuilder<string>();
for (int i = 0; i < 10_000; i++)
{
    builder.Add($"Item {i}");
}
ImmutableList<string> items = builder.ToImmutable();
```

Builders are also the way to convert efficiently from a mutable collection: `list.ToImmutableList()` and `dictionary.ToImmutableDictionary()` exist, but for large inputs a builder loop with `AddRange` avoids intermediate allocations.

## When Immutability Helps

- **Thread safety.** Immutable collections are safe to share across threads with no locking: a reader can never observe a half-finished write, because writes produce a new object instead of mutating the shared one. This is the main reason they appear in compilers (Roslyn uses them heavily) and in actor-based systems.
- **Snapshots and undo.** Keep the old version around and you get a free snapshot — an undo stack is just a list of previous versions, and structural sharing keeps the memory cost low.
- **Defensive APIs.** Returning an `ImmutableList<T>` from a method guarantees callers cannot corrupt your internal state, without the cost of copying on every call the way `ToList()` would.
- **Functional style.** LINQ-style pipelines that transform data step by step compose naturally when each step returns a new collection.

## When NOT to Use Them

Do not reach for immutable collections in a tight loop that updates a collection in place thousands of times — each `Add` allocates O(log n) new nodes, and the garbage collector will notice. A `List<T>` with a lock, or a `ConcurrentDictionary<K, V>`, is usually faster for hot mutable state. Also, `ImmutableArray<T>` is the wrong choice when the size changes often; it is designed for fixed-size data that is read many times.

## Key Takeaways

- Immutable collections never change in place; every update returns a new version that shares most nodes with the old one.
- Always capture the return value of `Add`, `Remove`, and `SetItem` — the original is untouched.
- Use builders (`CreateBuilder`) when constructing a large collection in a loop.
- The big wins are thread safety without locks, cheap snapshots/undo, and safe public APIs — not raw speed for in-place mutation.
