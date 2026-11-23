# Concurrent Collections in C#

When several threads share a collection, a plain `Dictionary` or `List` guarded by `lock` works, but it serializes every access and is easy to get wrong: check-then-act sequences (`if (!dict.ContainsKey(k)) dict[k] = v;`) race even inside a short lock if the lock is forgotten in one place. The concurrent collections in `System.Collections.Concurrent` are designed for this case: individual operations are atomic, and common compound operations have atomic methods of their own.

## ConcurrentDictionary

`ConcurrentDictionary<TKey, TValue>` is the thread-safe replacement for a locked `Dictionary`. Its key methods are atomic:

```csharp
using System.Collections.Concurrent;

var counts = new ConcurrentDictionary<string, int>();

// Adds 1 if "error" is absent, otherwise increments the existing value — atomically.
int newCount = counts.AddOrUpdate("error", 1, (key, oldValue) => oldValue + 1);

// Returns the existing value, or adds and returns the factory result — atomically.
int value = counts.GetOrAdd("warning", _ => 0);

// Safe read; returns false instead of throwing when the key is absent.
if (counts.TryGetValue("error", out int errorCount))
{
    Console.WriteLine(errorCount);
}

// Safe removal.
counts.TryRemove("warning", out _);
```

`AddOrUpdate` and `GetOrAdd` remove the classic race where two threads both see a missing key and both try to add it. Note that the value factory in `GetOrAdd` may run more than once under contention — it must be side-effect free, or the side effect must be harmless if repeated.

## ConcurrentQueue, ConcurrentStack, and ConcurrentBag

These three cover the common ordering patterns. All of them are lock-free or fine-grained-locked internally, and all expose `TryDequeue` / `TryPop` / `TryTake` that return `false` on an empty collection instead of throwing.

```csharp
var queue = new ConcurrentQueue<string>();
queue.Enqueue("first");
queue.Enqueue("second");

if (queue.TryDequeue(out string? next))
{
    Console.WriteLine(next); // "first" — FIFO order.
}

var stack = new ConcurrentStack<string>();
stack.Push("bottom");
stack.Push("top");

if (stack.TryPop(out string? top))
{
    Console.WriteLine(top); // "top" — LIFO order.
}

var bag = new ConcurrentBag<int>();
bag.Add(1);
bag.Add(2);

if (bag.TryTake(out int item))
{
    Console.WriteLine(item); // 1 or 2 — order is unspecified.
}
```

Use a queue for work items processed in arrival order, a stack for LIFO work such as undo buffers or depth-first traversal, and a bag only when order truly does not matter — its unordered, thread-local storage makes `Add`/`Take` fast but iteration order unpredictable. `TryPeek` reads the next item without removing it; it returns `false` when the collection is empty.

## BlockingCollection and Producer-Consumer

`BlockingCollection<T>` wraps any `IProducerConsumerCollection<T>` (a `ConcurrentQueue<T>` by default) and adds blocking and bounding. Consumers can wait for items instead of polling, and producers can be throttled by a capacity limit.

```csharp
using System.Collections.Concurrent;

// Bounded to 3 items: a 4th Add blocks until a consumer takes one.
var work = new BlockingCollection<string>(boundedCapacity: 3);

var producer = Task.Run(() =>
{
    for (int i = 1; i <= 5; i++)
    {
        work.Add($"job-{i}");
        Console.WriteLine($"Produced job-{i}");
    }
    work.CompleteAdding();
});

var consumer = Task.Run(() =>
{
    // GetConsumingEnumerable ends when CompleteAdding is called
    // and the collection is empty.
    foreach (string job in work.GetConsumingEnumerable())
    {
        Console.WriteLine($"Consumed {job}");
    }
});

await Task.WhenAll(producer, consumer);
// All 5 jobs are produced and consumed, in order job-1 .. job-5,
// even though the collection never holds more than 3 at once.
```

`CompleteAdding` is the signal that no more items will arrive; without it, `GetConsumingEnumerable` waits forever. `TryAdd` with a timeout and `TryTake` with a timeout are the non-blocking alternatives when a thread must not wait indefinitely.

## When to Use Them vs lock plus Dictionary or List

- Use a concurrent collection when many threads read and write the same collection and the operations are independent — counters, caches, work queues, per-thread accumulation.
- Use `lock` plus a plain collection when an operation spans several steps that must be atomic as a group, for example moving an item from one dictionary to another, or checking two collections together. A single concurrent collection cannot make a multi-collection update atomic.
- Use `lock` when iteration must see a stable snapshot: enumerating a `ConcurrentDictionary` is safe (it never throws), but it reflects a moment in time and may include items added during the enumeration or miss items removed during it.
- Never lock around a `BlockingCollection.Add` on a bounded collection while holding another lock the consumer needs — the producer blocks, the consumer cannot proceed, and the program deadlocks.

## Key Takeaways

- `ConcurrentDictionary` makes single-key operations atomic; `GetOrAdd` and `AddOrUpdate` replace racy check-then-act code, but the value factory must tolerate running twice.
- `ConcurrentQueue` is FIFO, `ConcurrentStack` is LIFO, `ConcurrentBag` is unordered; all use `Try*` methods that return `false` instead of throwing on empty.
- `BlockingCollection` adds blocking waits and a capacity bound on top of a concurrent queue, with `CompleteAdding` as the clean shutdown signal.
- Concurrent collections do not make multi-step or multi-collection updates atomic — that still needs a `lock`.
- Enumerating a concurrent collection is safe but gives a moment-in-time view, not a frozen snapshot.
