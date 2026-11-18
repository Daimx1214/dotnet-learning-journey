# Task vs ValueTask

`Task<T>` and `ValueTask<T>` both represent an asynchronous operation that will produce a value, and both can be awaited in exactly the same way. The difference is what they cost. `Task<T>` is a reference type — every async method that returns one allocates a `Task` object on the heap, even when the result is already available. `ValueTask<T>` is a struct that can hold the result directly, avoiding that allocation when the operation completes synchronously.

It matters because hot paths add up: a cache lookup, a fast validation, or a check that usually hits an in-memory value may complete synchronously 99% of the time. Returning `Task<T>` there allocates a heap object on every call; returning `ValueTask<T>` allocates nothing in the common case.

## The Allocation Difference

Consider a method that usually returns a cached value and only occasionally does real async work:

```csharp
// Task<T> version: allocates a Task object on EVERY call, even cache hits.
public Task<User?> GetUserAsync(int id)
{
    if (_cache.TryGetValue(id, out var user))
        return Task.FromResult<User?>(user); // still a heap allocation
    return LoadFromDatabaseAsync(id);
}
```

```csharp
// ValueTask<T> version: cache hits allocate nothing.
public ValueTask<User?> GetUserAsync(int id)
{
    if (_cache.TryGetValue(id, out var user))
        return new ValueTask<User?>(user); // no allocation
    return new ValueTask<User?>(LoadFromDatabaseAsync(id));
}
```

The saving only applies when the method often completes synchronously. If the method almost always awaits real I/O, `ValueTask<T>` buys nothing — the async state machine allocates anyway — and `Task<T>` is the simpler, safer choice.

## When ValueTask Wins

Use `ValueTask<T>` when **all three** are true:

1. The method is called very frequently (a hot path).
2. It often completes synchronously (cache hit, fast-path validation, already-completed check).
3. Profiling shows the allocations actually matter.

Good candidates:

```csharp
public ValueTask<int> GetCountAsync()
{
    if (_count.HasValue)
        return new ValueTask<int>(_count.Value);
    return new ValueTask<int>(LoadCountFromDatabaseAsync());
}
```

Bad candidates: a method that always hits the network, a method called rarely, or a public API where consumers might misuse the result (see the rules below). The .NET runtime itself uses `ValueTask<T>` in exactly these spots — `Stream.ReadAsync` returns `ValueTask<int>` because reads often complete synchronously from a buffer.

## The Rules — They Are Not Optional

`ValueTask<T>` is a struct with strict usage rules. Breaking them causes bugs that are subtle, intermittent, and hard to reproduce.

### Rule 1: Await it exactly once

```csharp
var result = GetUserAsync(42);
var user1 = await result; // OK
var user2 = await result; // BUG — awaiting the same ValueTask twice is undefined
```

A `ValueTask<T>` may wrap a pooled `IValueTaskSource`. After the first await, the underlying source can be recycled for another operation, so a second await may return someone else's result — or throw. If you need the result more than once, await once and store the value.

### Rule 2: Never store it in a field or pass it around

```csharp
// BUG: storing a ValueTask for later use.
private ValueTask<User?> _pendingUser;

public void StartLoad(int id) => _pendingUser = GetUserAsync(id);
public async Task<User?> FinishLoad() => await _pendingUser; // undefined behaviour
```

A `ValueTask` is only valid until it is awaited. Store the awaited result, or store a `Task<T>` — convert with `.AsTask()` if you genuinely need to hold on to the operation.

```csharp
// If you must store the operation, convert to Task first.
Task<User?> pending = GetUserAsync(42).AsTask();
```

### Rule 3: Do not await it after the method that produced it has returned in a different way

The same pooling reason applies: once the operation is complete and the `ValueTask` has been consumed (or the backing source recycled), the value is gone. Await it promptly, in the same logical flow that received it.

## ValueTask Without a Result

The non-generic `ValueTask` exists for the same reason — an async method with no return value that often completes synchronously:

```csharp
public ValueTask FlushIfNeededAsync()
{
    if (!_dirty)
        return ValueTask.CompletedTask; // no allocation
    return new ValueTask(SaveAsync());
}
```

## Async Method Guidance

For ordinary async methods, the defaults are simple:

| Situation | Return type |
|---|---|
| Normal async method (does I/O) | `Task` / `Task<T>` |
| Hot path, often completes synchronously | `ValueTask` / `ValueTask<T>` |
| Public API consumed by unknown callers | Prefer `Task` / `Task<T>` unless profiling justifies otherwise |
| Need to await the result multiple times | `Task<T>` (or await once and cache the value) |
| Need to store the operation | `Task<T>` (convert with `.AsTask()`) |

Inside an `async` method, the compiler handles the return type for you — an `async ValueTask<T>` method still has a state machine, but the completed-synchronous path avoids the `Task` allocation:

```csharp
public async ValueTask<User?> GetUserAsync(int id)
{
    if (_cache.TryGetValue(id, out var user))
        return user; // completes synchronously, no Task allocation

    return await LoadFromDatabaseAsync(id);
}
```

Note that `async ValueTask<T>` methods cannot use `await` on the returned value more than once either — the rule follows the type, not the syntax.

## A Realistic Benchmark Sense

On a cache-hit path called millions of times, switching from `Task<T>` to `ValueTask<T>` removes one heap allocation per call. That reduces GC pressure and can measurably improve throughput in a tight loop or a high-traffic API. On a path called a few times per request that always awaits the database, the difference is zero and the extra rules are pure risk. Measure first — `BenchmarkDotNet` with memory diagnostics (`[MemoryDiagnoser]`) shows the allocation difference directly.

## Key Takeaways

- `Task<T>` allocates a heap object on every call; `ValueTask<T>` is a struct that avoids the allocation when the operation completes synchronously.
- Use `ValueTask<T>` only on hot paths that often complete synchronously — cache hits, fast-path checks — and only when profiling shows it matters.
- Await a `ValueTask` exactly once: awaiting twice is undefined behaviour because the backing source may be pooled and recycled.
- Never store a `ValueTask` in a field or hold it for later; await it promptly, or convert to `Task` with `.AsTask()` if you must keep it.
- For ordinary async I/O methods and public APIs, `Task<T>` is the simpler and safer default.
- `ValueTask` (non-generic) serves the same purpose for async methods with no return value.
