# Volatile, Interlocked and Memory Barriers

When several threads share a variable, the value one thread writes is not guaranteed to be visible to another thread right away. The compiler and the CPU can reorder reads and writes, and each core can hold a stale copy in its cache. `volatile`, `Interlocked` and memory barriers are the tools that control this.

## The Problem: Reordering and Stale Reads

Consider a simple flag that tells a worker thread to stop:

```csharp
private bool _stopRequested;

public void RequestStop() => _stopRequested = true;

public void Work()
{
    while (!_stopRequested)
    {
        // do work
    }
}
```

On paper this looks fine. In practice the JIT compiler may hoist the read of `_stopRequested` out of the loop, turning it into an infinite loop, because nothing in the loop tells it the value can change. The CPU cache can also keep serving the old value. This is a real bug, not a theoretical one.

## The `volatile` Keyword

Marking a field `volatile` tells the compiler and the runtime that every read and write must go to memory and must not be reordered with respect to other volatile operations:

```csharp
private volatile bool _stopRequested;

public void RequestStop() => _stopRequested = true;

public void Work()
{
    while (!_stopRequested)
    {
        // do work
    }
}
```

Now the loop re-reads the flag from memory on every iteration, and `RequestStop` is visible to the worker thread promptly. `volatile` gives you acquire semantics on reads and release semantics on writes: a volatile read cannot move before earlier reads, and a volatile write cannot move after later writes.

`volatile` is allowed on these field types: `bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `char`, `float`, and reference types. It is not allowed on `long`, `double`, or `decimal`, and it cannot be applied to local variables.

## Why `volatile` Is Not Enough

`volatile` guarantees visibility and ordering, but it does not make an operation atomic. A read-modify-write sequence such as `_count++` is three steps (read, add, write), and two threads can interleave between them:

```csharp
private volatile int _count;

public void Increment()
{
    _count++; // NOT thread-safe: read, add, write can interleave
}
```

Two threads running `Increment` at the same time can both read 5, both add 1, and both write 6, losing one increment. `volatile` cannot fix this. Neither can it protect a multi-field invariant, such as updating a balance and a transaction count together. For those cases you need `Interlocked` or a `lock`.

## `Interlocked`: Atomic Operations

`Interlocked` provides atomic operations implemented with CPU instructions (compare-and-swap), so no lock is needed:

```csharp
private int _counter;

public void Increment() => Interlocked.Increment(ref _counter);

public void Decrement() => Interlocked.Decrement(ref _counter);

public int Add(int amount) => Interlocked.Add(ref _counter, amount);
```

`Interlocked.Increment` and `Interlocked.Decrement` return the new value. `Interlocked.Add` adds a value atomically and returns the result. These are the right tool for counters, statistics, and sequence numbers.

## CompareExchange: The Building Block

`Interlocked.CompareExchange` atomically sets a value only if it currently equals an expected value, and returns the original value:

```csharp
private int _state; // 0 = idle, 1 = running

public bool TryStart()
{
    // Set to 1 only if currently 0. Returns the value before the swap.
    return Interlocked.CompareExchange(ref _state, 1, 0) == 0;
}

public void Stop() => Interlocked.Exchange(ref _state, 0);
```

This pattern is how lock-free state machines are built. `CompareExchange` is also the core of lock-free stacks and queues: read the head, build a new node pointing at it, and swap the head only if it has not changed since you read it. If it changed, retry the whole sequence.

## Memory Barriers

A memory barrier (fence) forces ordering: no memory operation may cross it. `volatile` reads and writes imply half-fences, and `Interlocked` operations imply full fences. You can also place one explicitly with `Thread.MemoryBarrier()`, for example to make sure data writes are visible before a flag is set. In practice you rarely call it directly. `lock`, `Interlocked`, `volatile`, and the concurrent collections all place the fences for you.

## Lock-Free Counter Example

A complete, correct lock-free counter that many threads can hit safely:

```csharp
public sealed class LockFreeCounter
{
    private int _value;
    public int Increment() => Interlocked.Increment(ref _value);
    public int Add(int amount) => Interlocked.Add(ref _value, amount);
    // A plain read of an int is atomic; Volatile.Read adds the fence.
    public int Value => Volatile.Read(ref _value);
}
```

`Volatile.Read` and `Volatile.Write` are the modern, explicit way to get volatile semantics without declaring the field `volatile`. They work on `long` and reference types too, which the keyword does not cover. As a rule: a flag one thread sets and another reads takes `volatile`; a counter takes `Interlocked`; a one-time state transition takes `CompareExchange`; several fields that must change together take `lock`.

## Key Takeaways

- `volatile` fixes visibility and reordering for single reads and writes, but it does not make read-modify-write sequences atomic.
- `_count++` on a volatile field still loses increments under contention; use `Interlocked.Increment` instead.
- `Interlocked.CompareExchange` is the primitive behind lock-free data structures: swap only if the value is still what you expect, and retry otherwise.
- Every `Interlocked` operation is a full memory fence, so writes made before it are visible to threads that read after it.
- When several fields must change together as one unit, reach for `lock` or a concurrent collection, not `volatile`.
