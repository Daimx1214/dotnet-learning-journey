# ArrayPool<T> and Memory Pooling

Allocating a fresh array for every short-lived buffer puts pressure on the garbage collector: each allocation must eventually be collected, and large arrays can end up on the large object heap. `ArrayPool<T>.Shared` lends you an array from a shared pool instead, so repeated work reuses the same memory.

## Why Pooling Matters

Consider a server handling thousands of requests per second, each one renting a 4 KB scratch buffer. Without pooling, that is thousands of allocations per second that the GC must track and collect, which shows up as GC pauses and CPU spent collecting. With pooling, the same few arrays are rented, used, and returned, so the GC has almost nothing to do for that buffer. Pooling pays off when buffers are rented often and briefly; for a buffer you keep for the lifetime of the app, just allocate it once.

## The Correct Rent/Return Pattern

`Rent(n)` may return an array longer than `n` — never assume its length equals what you asked for. Use only the first `n` slots, and always return the array in a `finally` block so an exception cannot leak it. Pass `clearArray: true` when the buffer held sensitive data.

```csharp
using System.Buffers;

int[] buffer = ArrayPool<int>.Shared.Rent(10);
try
{
    for (int i = 0; i < 10; i++)
    {
        buffer[i] = i * i;
    }

    int sum = 0;
    for (int i = 0; i < 10; i++) // only the first 10 slots are ours
    {
        sum += buffer[i];
    }
    Console.WriteLine(sum); // 0+1+4+9+16+25+36+49+64+81 = 285
}
finally
{
    ArrayPool<int>.Shared.Return(buffer);
}
```

The printed sum is 285: the squares of 0 through 9 are 0, 1, 4, 9, 16, 25, 36, 49, 64, 81, and they add up to 285.

## MemoryPool<T> and Other Pools

`MemoryPool<T>.Shared` is the `Memory<T>`/`IMemoryOwner<T>` counterpart: rent it when an API wants a `Memory<T>` rather than a plain array, and dispose the owner (usually with `using`) to return the memory. The same rules apply — use only the requested length, and always return what you rented. Do not return an array twice, and never use a buffer after returning it; the pool may hand it to another caller immediately.

## Key Takeaways

- Rent short-lived buffers from `ArrayPool<T>.Shared` instead of allocating, to cut GC pressure in hot paths.
- `Rent(n)` can return a longer array — use only the first `n` elements and never rely on `buffer.Length`.
- Always return the array in a `finally` block; a leaked rental is memory the pool can never lend again.
- Use `clearArray: true` on return when the buffer held secrets, and never touch a buffer after returning it.
- `MemoryPool<T>.Shared` is the equivalent for `Memory<T>`-based APIs; dispose its owner to return the memory.
