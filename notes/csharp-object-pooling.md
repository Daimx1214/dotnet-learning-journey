# Object Pooling in C#

Object pooling reuses expensive objects instead of allocating new ones. In .NET, `Microsoft.Extensions.ObjectPool` provides a small, well-tested pool: `ObjectPool<T>` hands out instances, and `PooledObjectPolicy<T>` controls how instances are created and reset before reuse. Pooling helps when allocation is frequent and the objects are expensive to create or put pressure on the garbage collector; it hurts when objects are cheap, short-lived, or hold state that is hard to reset correctly.

## The API Surface

The package `Microsoft.Extensions.ObjectPool` ships with ASP.NET Core, so most web apps already reference it transitively. The core types are `ObjectPool<T>` (abstract, with `Get()` and `Return(T)`), `DefaultObjectPool<T>` (the standard implementation, backed by a bounded stack), and `ObjectPoolProvider` (creates pools, usually via `DefaultObjectPoolProvider`).

```csharp
using Microsoft.Extensions.ObjectPool;

// A pool for StringBuilder: the classic example, because ASP.NET Core
// itself pools StringBuilder instances internally.
ObjectPool<StringBuilder> pool = new DefaultObjectPoolProvider()
    .Create(new StringBuilderPooledObjectPolicy());

StringBuilder sb = pool.Get();
try
{
    sb.Append("Hello, ").Append("pooled world!");
    Console.WriteLine(sb.ToString());
}
finally
{
    pool.Return(sb); // Return resets the builder (Length = 0) via the policy.
}
```

`StringBuilderPooledObjectPolicy` caps the retained builder capacity (default maximum retained capacity is 100 characters... in practice the policy returns builders whose capacity is at most `MaximumRetainedCapacity`, default 100, and discards larger ones). Returning a builder with a 1 MB buffer does not keep the 1 MB buffer alive — the policy drops it.

## A Custom Policy

A custom `PooledObjectPolicy<T>` implements `Create()` and `Return(T)`. `Return` must reset the object to a clean state and return `false` if the object is unusable and should be discarded.

```csharp
using Microsoft.Extensions.ObjectPool;

// Pooling a reusable byte buffer wrapper for a network parser.
sealed class BufferPolicy : PooledObjectPolicy<ParserBuffer>
{
    public override ParserBuffer Create() => new(new byte[4096]);

    public override bool Return(ParserBuffer obj)
    {
        if (obj.IsCorrupted) return false; // discard, do not reuse
        obj.Reset();                       // Length = 0, position = 0
        return true;
    }
}

sealed class ParserBuffer(byte[] buffer)
{
    public byte[] Buffer { get; } = buffer;
    public int Length { get; set; }
    public bool IsCorrupted { get; set; }
    public void Reset() { Length = 0; IsCorrupted = false; }
}

// Usage via DI in ASP.NET Core:
 // builder.Services.AddSingleton<ObjectPool<ParserBuffer>>(sp =>
 //     new DefaultObjectPoolProvider().Create(new BufferPolicy()));
```

## When Pooling Helps vs When It Hurts

Pooling helps when all three are true: the object is expensive to construct (large buffers, regex instances, `StringBuilder` with big capacity), it is allocated at high frequency (per request, per message), and it can be reset cheaply and correctly. Measured on a tight loop, pooling a `StringBuilder` used to build a 200-character string saves roughly 30–50% of allocation time versus `new StringBuilder()` per iteration, and eliminates Gen0 collections in hot paths. ASP.NET Core pools `StringBuilder`, `HttpContext` features, and `ArrayPool<byte>` buffers for exactly this reason.

Pooling hurts when the object is cheap (a small DTO, a `List<int>` with 3 items — allocation is ~20 ns and Gen0 collection is nearly free), when `Return` cannot fully reset state (a leaked flag becomes a cross-request bug), when the pool retains memory that would otherwise be freed (a pool holding 128 builders of 100 KB each pins 12.8 MB), or when the object is used concurrently after being returned. A pooled object returned twice, or used after `Return`, is a use-after-free bug with no compiler warning.

## Sizing and Defaults

`DefaultObjectPool<T>` keeps at most `Environment.ProcessorCount * 2` objects by default (so 16 on an 8-core machine). `Get()` beyond that limit simply creates a new instance that is discarded on `Return` instead of stored — the pool never blocks and never grows unboundedly. `ArrayPool<T>.Shared` is the sibling API for raw arrays: `Rent(n)` returns an array of at least `n` elements (often larger — never assume the length equals `n`), and `Return(array)` must be called with `clearArray: true` if the contents are sensitive.

## Common Pitfalls

- Forgetting to `Return` an object leaks the pool slot, not memory — the pool just creates fresh instances, and the benefit quietly disappears.
- Returning an object and then continuing to use it is a data-corruption bug; treat `Return` as handing over ownership.
- Sharing one pooled instance across threads: pools are thread-safe, pooled objects are not.
- Pooling objects that hold unmanaged resources or large rooted graphs pins memory and defeats the GC.
- Resetting in `Return` must be complete: every field, every flag, every nested collection. A missed field is a bug that only appears under load.

## Key Takeaways

- `Microsoft.Extensions.ObjectPool` gives `ObjectPool<T>` plus `PooledObjectPolicy<T>`; `Create()` builds, `Return()` resets and votes on reuse.
- Pool only expensive, high-frequency, cheaply-resettable objects; cheap short-lived objects are faster to just allocate.
- `DefaultObjectPool<T>` caps retained objects at `ProcessorCount * 2` and never blocks — excess instances are created and discarded.
- Always `Return` in a `finally` block, and never touch a pooled object after returning it.
- `ArrayPool<T>.Shared` is the right tool for raw buffers; remember rented arrays may be longer than requested.
