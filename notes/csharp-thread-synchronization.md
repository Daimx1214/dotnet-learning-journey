# Thread Synchronization in C#

When several threads share mutable state, only one may change it at a time — otherwise updates are lost or data structures corrupt. C# offers several synchronization primitives; the right one depends on what you are protecting and for how long.

## lock and Monitor

`lock` is the default choice for protecting a short critical section on shared in-memory state. It is syntactic sugar over `Monitor.Enter`/`Exit`, is re-entrant on the same thread, and always releases the lock when the block exits, even on an exception. Lock on a private object, never on `this`, a string, or a type — those can be locked by outside code and deadlock you.

```csharp
private readonly object _gate = new();
private int _counter;

public void Increment()
{
    lock (_gate)
    {
        _counter++;
    }
}
```

## Interlocked for Simple Counters

When the whole operation is a single increment, decrement, or exchange, `Interlocked` is faster than a lock because it uses one atomic CPU instruction and never blocks a thread. Use it for counters, flags, and simple accumulations — not for multi-step updates that must happen together.

```csharp
private int _requests;

public void RecordRequest() => Interlocked.Increment(ref _requests);
public int Requests => Volatile.Read(ref _requests);
```

## SemaphoreSlim and ReaderWriterLockSlim

`SemaphoreSlim` limits how many threads may enter at once, and unlike `lock` it works with `await` (`WaitAsync`), so it is the right choice for throttling async work such as outgoing HTTP calls. `ReaderWriterLockSlim` allows many concurrent readers but only one writer, which pays off when reads vastly outnumber writes and the protected data is read often.

```csharp
// Throttle to 3 concurrent async operations.
private readonly SemaphoreSlim _throttle = new(3, 3);

public async Task<string> FetchAsync(string url)
{
    await _throttle.WaitAsync();
    try
    {
        using var client = new HttpClient();
        return await client.GetStringAsync(url);
    }
    finally
    {
        _throttle.Release();
    }
}
```

## Which to Pick When

Use `Interlocked` for a lone counter or flag. Use `lock` for short, synchronous critical sections over shared state. Use `SemaphoreSlim` when you need to cap concurrency or wait asynchronously. Use `ReaderWriterLockSlim` for read-heavy shared data with rare writes. Keep every critical section as short as possible, never call unknown code (events, callbacks, virtual methods) while holding a lock, and never hold a lock across an `await` — `lock` cannot span an await, which is exactly the case `SemaphoreSlim` exists for.

## Key Takeaways

- `lock` (Monitor) is the default for short synchronous critical sections; lock a private object, never `this`.
- `Interlocked.Increment` handles single-step counter updates atomically, faster and simpler than a lock.
- `SemaphoreSlim` caps concurrency and supports `await`, making it the async-friendly primitive.
- `ReaderWriterLockSlim` lets many readers in at once but only one writer — best for read-heavy data.
- Keep critical sections tiny, never await inside a `lock`, and never call unknown code while holding one.
